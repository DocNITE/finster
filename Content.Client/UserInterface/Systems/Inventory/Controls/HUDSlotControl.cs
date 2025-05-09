using System.Numerics;
using Content.Client._ViewportGui.ViewportUserInterface;
using Content.Client._ViewportGui.ViewportUserInterface.UI;
using Content.Client.UserInterface.Systems.Inventory.Controls;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.UserInterface.Systems.Inventory.Controls;

/// <summary>
/// Used for inventory and (maybe) another systems.
/// I HATE THAK INV CODE! I HATE THAK INV CODE! I HATE THAK INV CODE!
/// I HATE THAK INV CODE! I HATE THAK INV CODE! I HATE THAK INV CODE!
/// I HATE THAK INV CODE! I HATE THAK INV CODE! I HATE THAK INV CODE!
/// WizDen can go fuck himself
/// </summary>
public class HUDSlotControl : HUDButton, IEntityControl
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IViewportUserInterfaceManager _vpUIManager = default!;

    private Texture? _buttonTexture;

    public static int DefaultButtonSize = 32;
    public const string SlotButtonPrefix = "SlotButton_";
    public const string HoverNamePrefix = "slotbutton-";

    public HUDTextureRect BlockedRect { get; }
    public HUDAnimatedTextureRect HighlightRect { get; }

    public EntityUid? Entity;
    EntityUid? IEntityControl.UiEntity => Entity;

    private bool _slotNameSet;

    private string _slotName = "";
    public string SlotName
    {
        get => _slotName;
        set
        {
            //this auto registers the button with it's parent container when it's set
            if (_slotNameSet)
            {
                Logger.Warning("Tried to set slotName after init for:" + Name);
                return;
            }
            _slotNameSet = true;
            Name = SlotButtonPrefix + value;
            _slotName = value;
        }
    }

    public string HoverName { get; set; } = string.Empty;

    public string HUDSlotGroup { get; set; } = "Default";

    public bool Highlight { get => HighlightRect.Visible; set => HighlightRect.Visible = value; }

    public bool Blocked { get => BlockedRect.Visible; set => BlockedRect.Visible = value; }

    private string? _blockedTexturePath;
    public string? BlockedTexturePath
    {
        get => _blockedTexturePath;
        set
        {
            _blockedTexturePath = value;
            BlockedRect.Texture = _uiManager.CurrentTheme.ResolveTextureOrNull(value)?.Texture;
        }
    }

    private string? _buttonTexturePath;
    public string? ButtonTexturePath
    {
        get => _buttonTexturePath;
        set
        {
            _buttonTexturePath = value;
            UpdateButtonTexture();
        }
    }

    private string? _fullButtonTexturePath;
    public string? FullButtonTexturePath
    {
        get => _fullButtonTexturePath;
        set
        {
            _fullButtonTexturePath = value;
            UpdateButtonTexture();
        }
    }

    private string? _highlightTexturePath;
    public string? HighlightTexturePath
    {
        get => _highlightTexturePath;
        set
        {
            _highlightTexturePath = value;
            if (value is null)
                return;
            HighlightRect.SetFromSpriteSpecifier(_vpUIManager.GetThemeRsi(value, "highlight"));
        }
    }

    public event Action<GUIBoundKeyEventArgs, HUDSlotControl>? Pressed;
    public event Action<GUIBoundKeyEventArgs, HUDSlotControl>? Unpressed;
    public event Action<GUIBoundKeyEventArgs, HUDSlotControl>? Hover;

    public bool MouseIsHovering;

    public HUDSlotControl()
    {
        IoCManager.InjectDependencies(this);
        Name = "SlotButton_null";
        Size = (DefaultButtonSize, DefaultButtonSize);
        CanEmitSound = false;

        AddChild(BlockedRect = new HUDTextureRect
        {
            Visible = false,
            Size = (DefaultButtonSize, DefaultButtonSize)
        });

        AddChild(HighlightRect = new HUDAnimatedTextureRect
        {
            Visible = false,
            Size = (DefaultButtonSize, DefaultButtonSize)
        });

        HighlightTexturePath = "Slots/highlight.rsi";
        BlockedTexturePath = "blocked";

        OnKeyBindDown += OnButtonPressed;
        OnKeyBindUp += OnButtonUnpressed;
    }

    public override void Draw(in ViewportUIDrawArgs args)
    {
        var handle = args.ScreenHandle;

        if (_buttonTexture is null || !Visible)
        {
            base.Draw(args);
            return;
        }

        handle.DrawTextureRect(_buttonTexture, new UIBox2(GlobalPosition, GlobalPosition + Size));

        base.Draw(args);

        if (Entity is not null)
        {
            var spriteSystem = _entManager.System<SpriteSystem>();
            spriteSystem.ForceUpdate((EntityUid) Entity);

            handle.DrawEntity(
                (EntityUid) Entity,
                GlobalPosition + (Size / 2),
                new Vector2(1f, 1f),
                Angle.Zero,
                Angle.Zero,
                Direction.South);
        }
    }

    public void SetEntity(EntityUid? ent)
    {
        Entity = ent;
        UpdateButtonTexture();
    }

    private void UpdateButtonTexture()
    {
        var fullTexture = _uiManager.CurrentTheme.ResolveTextureOrNull(_fullButtonTexturePath);
        var texture = Entity.HasValue && fullTexture != null
            ? fullTexture.Texture
            : _uiManager.CurrentTheme.ResolveTextureOrNull(_buttonTexturePath)?.Texture;
        _buttonTexture = texture;
    }

    private void OnButtonPressed(GUIBoundKeyEventArgs args)
    {
        Pressed?.Invoke(args, this);
    }

    private void OnButtonUnpressed(GUIBoundKeyEventArgs args)
    {
        Unpressed?.Invoke(args, this);
    }

    private void OnButtonHover(GUIBoundKeyEventArgs args)
    {
        Hover?.Invoke(args, this);
    }

    public void ClearHover()
    {
    }
}
