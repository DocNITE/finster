using System.Linq;
using Content.Client._Finster.Popups;
using Content.Client.Chat.Managers;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Replays;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Popups
{
    public sealed class PopupSystem : SharedPopupSystem
    {
        [Dependency] private readonly IConfigurationManager _configManager = default!;
        [Dependency] private readonly IInputManager _inputManager = default!;
        [Dependency] private readonly IOverlayManager _overlay = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IPrototypeManager _prototype = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
        [Dependency] private readonly IReplayRecordingManager _replayRecording = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly ExamineSystemShared _examine = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly PopupMessageSystem _popupMessage = default!;

        public IReadOnlyList<WorldPopupLabel> WorldLabels => _aliveWorldLabels;
        public IReadOnlyList<CursorPopupLabel> CursorLabels => _aliveCursorLabels;

        private readonly List<WorldPopupLabel> _aliveWorldLabels = new();
        private readonly List<CursorPopupLabel> _aliveCursorLabels = new();

        public const float MinimumPopupLifetime = 0.7f;
        public const float MaximumPopupLifetime = 5f;
        public const float PopupLifetimePerCharacter = 0.04f;

        private bool isLogging;

        private bool worldPopupsEnabled;

        public override void Initialize()
        {
            SubscribeNetworkEvent<PopupCursorEvent>(OnPopupCursorEvent);
            SubscribeNetworkEvent<PopupCoordinatesEvent>(OnPopupCoordinatesEvent);
            SubscribeNetworkEvent<PopupEntityEvent>(OnPopupEntityEvent);
            SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
            _overlay
                .AddOverlay(new PopupOverlay(
                    _configManager,
                    EntityManager,
                    _playerManager,
                    _prototype,
                    _uiManager,
                    _uiManager.GetUIController<PopupUIController>(),
                    _examine,
                    _transform,
                    this));

            isLogging = _configManager.GetCVar(CCVars.LogChatActions);
            _configManager.OnValueChanged(CCVars.LogChatActions, (log) => { isLogging = log; });

            worldPopupsEnabled = _configManager.GetCVar(CCVars.PopupMessages);
            _configManager.OnValueChanged(CCVars.PopupMessages, (val) => { worldPopupsEnabled = val; });
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _overlay
                .RemoveOverlay<PopupOverlay>();
        }

        private void PopupMessage(string? message, PopupType type, EntityCoordinates coordinates, EntityUid? entity, EntityUid? origin = null, bool recordReplay = true)
        {
            if (message == null)
                return;

            // Should we show popups on viewport.
            if (worldPopupsEnabled)
            {
                if (recordReplay && _replayRecording.IsRecording)
                {
                    if (entity != null)
                        _replayRecording.RecordClientMessage(new PopupEntityEvent(message, type, GetNetEntity(entity.Value)));
                    else
                        _replayRecording.RecordClientMessage(new PopupCoordinatesEvent(message, type, GetNetCoordinates(coordinates)));
                }

                var label = new WorldPopupLabel(coordinates)
                {
                    Text = message,
                    Type = type,
                };

                _aliveWorldLabels.Add(label);
            }

            // Should we log popups into chat.
            if (isLogging)
            {
                // TODO: Looks dirty, but it should combined into single system or something like
                if (origin.HasValue)
                    _popupMessage.DoIconMessage(origin.Value, message, type);
                else
                    _popupMessage.DoMessage(message, type);
            }
        }

        #region Abstract Method Implementations
        public override void PopupCoordinates(string? message, EntityCoordinates coordinates, PopupType type = PopupType.Small)
        {
            PopupMessage(message, type, coordinates, null);
        }

        public override void PopupCoordinates(string? message, EntityCoordinates coordinates, ICommonSession recipient, PopupType type = PopupType.Small)
        {
            if (_playerManager.LocalSession == recipient)
                PopupMessage(message, type, coordinates, null);
        }

        public override void PopupCoordinates(string? message, EntityCoordinates coordinates, EntityUid recipient, PopupType type = PopupType.Small)
        {
            if (_playerManager.LocalEntity == recipient)
                PopupMessage(message, type, coordinates, null);
        }

        private void PopupCursorInternal(string? message, PopupType type, bool recordReplay)
        {
            if (message == null)
                return;

            // Should we show popups on viewport.
            if (worldPopupsEnabled)
            {
                if (recordReplay && _replayRecording.IsRecording)
                    _replayRecording.RecordClientMessage(new PopupCursorEvent(message, type));

                var label = new CursorPopupLabel(_inputManager.MouseScreenPosition)
                {
                    Text = message,
                    Type = type,
                };

                _aliveCursorLabels.Add(label);
            }

            // Should we log popups into chat.
            if (isLogging)
            {
                _popupMessage.DoMessage(message, type);
            }
        }

        public override void PopupCursor(string? message, PopupType type = PopupType.Small)
            => PopupCursorInternal(message, type, true);

        public override void PopupCursor(string? message, ICommonSession recipient, PopupType type = PopupType.Small)
        {
            if (_playerManager.LocalSession == recipient)
                PopupCursor(message, type);
        }

        public override void PopupCursor(string? message, EntityUid recipient, PopupType type = PopupType.Small)
        {
            if (_playerManager.LocalEntity == recipient)
                PopupCursor(message, type);
        }

        public override void PopupCoordinates(string? message, EntityCoordinates coordinates, Filter filter, bool replayRecord, PopupType type = PopupType.Small)
        {
            PopupCoordinates(message, coordinates, type);
        }

        public override void PopupEntity(string? message, EntityUid uid, EntityUid recipient, PopupType type = PopupType.Small)
        {
            if (_playerManager.LocalEntity == recipient)
                PopupEntity(message, uid, type);
        }

        public override void PopupEntity(string? message, EntityUid uid, ICommonSession recipient, PopupType type = PopupType.Small)
        {
            if (_playerManager.LocalSession == recipient)
                PopupEntity(message, uid, type);
        }

        public override void PopupEntity(string? message, EntityUid uid, Filter filter, bool recordReplay, PopupType type = PopupType.Small)
        {
            if (!filter.Recipients.Contains(_playerManager.LocalSession))
                return;

            PopupEntity(message, uid, type);
        }

        public override void PopupClient(string? message, EntityUid? recipient, PopupType type = PopupType.Small)
        {
            if (recipient == null)
                return;

            if (_timing.IsFirstTimePredicted)
                PopupCursor(message, recipient.Value, type);
        }

        public override void PopupClient(string? message, EntityUid uid, EntityUid? recipient, PopupType type = PopupType.Small)
        {
            if (recipient == null)
                return;

            if (_timing.IsFirstTimePredicted)
                PopupEntity(message, uid, recipient.Value, type);
        }

        public override void PopupClient(string? message, EntityCoordinates coordinates, EntityUid? recipient, PopupType type = PopupType.Small)
        {
            if (recipient == null)
                return;

            if (_timing.IsFirstTimePredicted)
                PopupCoordinates(message, coordinates, recipient.Value, type);
        }

        public override void PopupEntity(string? message, EntityUid uid, PopupType type = PopupType.Small)
        {
            if (TryComp(uid, out TransformComponent? transform))
                PopupMessage(message, type, transform.Coordinates, uid);
        }

        public override void PopupPredicted(string? message, EntityUid uid, EntityUid? recipient, PopupType type = PopupType.Small)
        {
            if (recipient != null && _timing.IsFirstTimePredicted)
                PopupEntity(message, uid, recipient.Value, type);
        }

        public override void PopupPredicted(string? recipientMessage, string? othersMessage, EntityUid uid, EntityUid? recipient, PopupType type = PopupType.Small)
        {
            if (recipient != null && _timing.IsFirstTimePredicted)
                PopupEntity(recipientMessage, uid, recipient.Value, type);
        }

        #endregion

        #region Network Event Handlers

        private void OnPopupCursorEvent(PopupCursorEvent ev)
        {
            PopupCursorInternal(ev.Message, ev.Type, false);
        }

        private void OnPopupCoordinatesEvent(PopupCoordinatesEvent ev)
        {
            PopupMessage(ev.Message, ev.Type, GetCoordinates(ev.Coordinates), null, GetEntity(ev.Origin), false);
        }

        private void OnPopupEntityEvent(PopupEntityEvent ev)
        {
            var entity = GetEntity(ev.Uid);

            if (TryComp(entity, out TransformComponent? transform))
                PopupMessage(ev.Message, ev.Type, transform.Coordinates, entity, GetEntity(ev.Origin), false);
        }

        private void OnRoundRestart(RoundRestartCleanupEvent ev)
        {
            _aliveCursorLabels.Clear();
            _aliveWorldLabels.Clear();
        }

        #endregion

        public static float GetPopupLifetime(PopupLabel label)
        {
            return Math.Clamp(PopupLifetimePerCharacter * label.Text.Length,
                MinimumPopupLifetime,
                MaximumPopupLifetime);
        }

        public override void FrameUpdate(float frameTime)
        {
            if (_aliveWorldLabels.Count == 0 && _aliveCursorLabels.Count == 0)
                return;

            for (var i = 0; i < _aliveWorldLabels.Count; i++)
            {
                var label = _aliveWorldLabels[i];
                label.TotalTime += frameTime;

                if (label.TotalTime > GetPopupLifetime(label) || Deleted(label.InitialPos.EntityId))
                {
                    _aliveWorldLabels.RemoveSwap(i);
                    i--;
                }
            }

            for (var i = 0; i < _aliveCursorLabels.Count; i++)
            {
                var label = _aliveCursorLabels[i];
                label.TotalTime += frameTime;

                if (label.TotalTime > GetPopupLifetime(label))
                {
                    _aliveCursorLabels.RemoveSwap(i);
                    i--;
                }
            }
        }

        public abstract class PopupLabel
        {
            public PopupType Type = PopupType.Small;
            public string Text { get; set; } = string.Empty;
            public float TotalTime { get; set; }
        }

        public sealed class CursorPopupLabel : PopupLabel
        {
            public ScreenCoordinates InitialPos;

            public CursorPopupLabel(ScreenCoordinates screenCoords)
            {
                InitialPos = screenCoords;
            }
        }

        public sealed class WorldPopupLabel : PopupLabel
        {
            /// <summary>
            /// The original EntityCoordinates of the label.
            /// </summary>
            public EntityCoordinates InitialPos;

            public WorldPopupLabel(EntityCoordinates coordinates)
            {
                InitialPos = coordinates;
            }
        }
    }
}
