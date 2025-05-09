using System.Linq;
using Content.Client.Gameplay;
using Content.Client.RandomRules;
using Content.Shared.Audio;
using Content.Shared.CCVar;
using Content.Shared.Random;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.Audio;

public sealed class IngameMusicSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IStateManager _state = default!;
    [Dependency] private readonly RulesSystem _rules = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ContentAudioSystem _contentAudio = default!;

    private const float MusicFadeTime = 5f;
    private static float _volumeSlider;

    private EntityUid? _currentMusicStream;
    private IngameMusicPrototype? _currentMusic;
    private bool _interruptable;

    private readonly Dictionary<string, List<ResPath>> _musicTracks = new();
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_configManager, CCVars.IngameMusicVolume, MusicVolumeChanged, true);
        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("audio.ingame_music");

        SetupMusicTracks();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);
        _state.OnStateChanged += OnStateChange;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _state.OnStateChanged -= OnStateChange;
        StopCurrentMusic();
    }

    private void MusicVolumeChanged(float volume)
    {
        _volumeSlider = SharedAudioSystem.GainToVolume(volume);

        if (_currentMusicStream != null && _currentMusic != null)
        {
            _audio.SetVolume(_currentMusicStream, _currentMusic.Sound.Params.Volume + _volumeSlider);
        }
    }

    private void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<IngameMusicPrototype>())
            SetupMusicTracks();
    }

    private void OnStateChange(StateChangedEventArgs args)
    {
        if (args.NewState is GameplayState)
        {
            UpdateMusic();
        }
        else
        {
            StopCurrentMusic();
        }
    }

    private void SetupMusicTracks()
    {
        _musicTracks.Clear();
        foreach (var musicProto in _proto.EnumeratePrototypes<IngameMusicPrototype>())
        {
            var tracks = _musicTracks.GetOrNew(musicProto.ID);
            RefreshTracks(musicProto.Sound, tracks, null);
            _random.Shuffle(tracks);
        }
    }

    private void RefreshTracks(SoundSpecifier sound, List<ResPath> tracks, ResPath? lastPlayed)
    {
        DebugTools.Assert(tracks.Count == 0);

        switch (sound)
        {
            case SoundCollectionSpecifier collection:
                if (collection.Collection == null)
                    break;

                var soundCollection = _proto.Index<SoundCollectionPrototype>(collection.Collection);
                tracks.AddRange(soundCollection.PickFiles);
                break;
            case SoundPathSpecifier path:
                tracks.Add(path.Path);
                break;
        }

        if (tracks.Count > 1 && tracks[^1] == lastPlayed)
        {
            (tracks[0], tracks[^1]) = (tracks[^1], tracks[0]);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_state.CurrentState is not GameplayState)
            return;

        UpdateMusic();
    }

    private void UpdateMusic()
    {
        bool? isDone = null;

        if (TryComp(_currentMusicStream, out AudioComponent? audioComp))
        {
            isDone = !audioComp.Playing;
        }

        if (_interruptable)
        {
            var player = _player.LocalSession?.AttachedEntity;

            if (player == null || _currentMusic == null ||
                !_rules.IsTrue(player.Value, _proto.Index<RulesPrototype>(_currentMusic.Rules)))
            {
                FadeOutCurrentMusic();
                _currentMusic = null;
                isDone = true;
            }
        }

        if (isDone == false)
            return;

        var newMusic = GetNextMusic();

        if (newMusic == null)
        {
            _sawmill.Warning("No suitable ingame music found!");
            return;
        }

        PlayMusic(newMusic);
    }

    private void PlayMusic(IngameMusicPrototype musicProto)
    {
        FadeOutCurrentMusic();

        _currentMusic = musicProto;
        _interruptable = musicProto.Interruptable;

        var tracks = _musicTracks[musicProto.ID];
        var track = tracks[^1];
        tracks.RemoveAt(tracks.Count - 1);

        var audioParams = AudioParams.Default
            .WithVolume(musicProto.Sound.Params.Volume + _volumeSlider);
            //.WithLoop(true);

        var strim = _audio.PlayGlobal(
            track.ToString(),
            Filter.Local(),
            false,
            audioParams)!;

        _currentMusicStream = strim.Value.Entity;

        if (musicProto.FadeIn)
        {
            _contentAudio.FadeIn(_currentMusicStream, strim.Value.Component, MusicFadeTime);
        }

        // Update list if track is end
        if (tracks.Count == 0)
        {
            RefreshTracks(musicProto.Sound, tracks, track);
        }
    }

    private IngameMusicPrototype? GetNextMusic()
    {
        var player = _player.LocalEntity;

        if (player == null)
            return null;

        var ev = new PlayIngameMusicEvent();
        RaiseLocalEvent(ref ev);

        if (ev.Cancelled)
            return null;

        var allMusic = _proto.EnumeratePrototypes<IngameMusicPrototype>().ToList();
        allMusic.Sort((x, y) => y.Priority.CompareTo(x.Priority));

        foreach (var music in allMusic)
        {
            if (!_rules.IsTrue(player.Value, _proto.Index<RulesPrototype>(music.Rules)))
                continue;

            return music;
        }

        return null;
    }

    private void FadeOutCurrentMusic()
    {
        if (_currentMusicStream != null)
        {
            _contentAudio.FadeOut(_currentMusicStream, duration: MusicFadeTime);
            _currentMusicStream = null;
        }
    }

    private void StopCurrentMusic()
    {
        _currentMusicStream = _audio.Stop(_currentMusicStream);
        _currentMusic = null;
    }
}

/// <summary>
/// Raised whenever ingame music tries to play.
/// </summary>
[ByRefEvent]
public record struct PlayIngameMusicEvent(bool Cancelled = false);
