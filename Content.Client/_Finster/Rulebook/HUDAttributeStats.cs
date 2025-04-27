using System.Numerics;
using Content.Client._ViewportGui.ViewportUserInterface;
using Content.Client._ViewportGui.ViewportUserInterface.UI;
using Content.Shared._Finster.Rulebook;
using Content.Shared.Damage.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;

namespace Content.Client._Finster.Rulebook;

/// <summary>
/// Show the character attributes stats
/// </summary>
public class HUDAttributeStats : HUDTextureRect, IHUDDescription
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IViewportUserInterfaceManager _vpUIManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    private string _fontPath = "/Fonts/Bedstead/Bedstead.otf";
    private Font _font;

    private readonly DiceSystem _rpSystem;

    /// <summary>
    /// Return current font path or set a new font with the path.
    /// </summary>
    public string FontPath
    {
        get => _fontPath;
        set
        {
            _fontPath = value;
            _font = new VectorFont(_cache.GetResource<FontResource>(_fontPath), Scale);
        }
    }

    /// <summary>
    /// Text's font scale.
    /// </summary>
    public int Scale { get; set; } = 6;

    public string Description { get; set; }

    public HUDAnimatedTextureRect StaminaBar { get; set; }
    public HUDAnimatedTextureRect FatigueBar { get; set; }

    public HUDAttributeStats()
    {
        IoCManager.InjectDependencies(this);

        _font = new VectorFont(_cache.GetResource<FontResource>(_fontPath), Scale);
        _rpSystem = _entityManager.System<DiceSystem>();

        StaminaBar = new HUDAnimatedTextureRect();
        StaminaBar.SetFromSpriteSpecifier(_vpUIManager.GetThemeRsi("Stats/progress_bar.rsi", "stamina"));
        StaminaBar.Size = (0, 0);
        AddChild(StaminaBar);

        FatigueBar = new HUDAnimatedTextureRect();
        FatigueBar.SetFromSpriteSpecifier(_vpUIManager.GetThemeRsi("Stats/progress_bar.rsi", "fatigue"));

        FatigueBar.Size = (0, 0);
        AddChild(FatigueBar);

        Name = "AttributeStats";
        Description = "hud-desc-attribute-stats";
        Size = (32, 96);
        Position = (0, 224);
        Texture = _vpUIManager.GetThemeTexture("Stats/back.png");
    }

    public override void Draw(in ViewportUIDrawArgs args)
    {
        if (!VisibleInTree)
            return;

        DrawProgressBars(args);
        base.Draw(args);
        DrawContent(args);
    }

    private void DrawContent(in ViewportUIDrawArgs args)
    {
        var playerUid = _player.LocalEntity;
        if (playerUid is null)
            return;

        var handle = args.ScreenHandle;

        var currentY = GlobalPosition.Y;
        var currentX = GlobalPosition.X + Size.X - 12;
        var maxAttributes = (int) Attributes.Max;
        for (int i = 0; i < maxAttributes; i++)
        {
            if (!_rpSystem.TryGetAttributePoints(playerUid.Value, (Attributes) i, out var points))
                continue;

            var pointsStr = points.ToString();
            var dimensions = handle.GetDimensions(_font, pointsStr, 1f);
            handle.DrawString(_font,
                new Vector2(currentX - dimensions.X, currentY + 7),
                pointsStr,
                1f,
                Color.Gainsboro.WithAlpha(0.65f)); // TODO: Add effects color - is too debuffed then is red, if is too buffed is green

            currentY += 16;
        }
    }

    private void DrawProgressBars(in ViewportUIDrawArgs args)
    {
        var playerUid = _player.LocalEntity;
        if (playerUid is null)
            return;

        if (!_entityManager.TryGetComponent<StaminaComponent>(playerUid, out var stamina))
            return;

        // TODO: Add fatigue visualization

        var staminaDamageHeightPx =
            (int) ((stamina.StaminaDamage / stamina.CritThreshold) * Size.Y);
        StaminaBar.Position = (0, staminaDamageHeightPx);
        StaminaBar.Size = (32, Size.Y - staminaDamageHeightPx);
    }
}
