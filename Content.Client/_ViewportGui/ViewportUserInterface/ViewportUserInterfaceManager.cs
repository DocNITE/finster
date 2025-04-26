using System.Linq;
using System.Numerics;
using Content.Client._ViewportGui.ViewportUserInterface.UI;
using Content.Client.Resources;
using Content.Client.Viewport;
using Content.KayMisaZlevels.Client;
using Content.Shared.CCVar;
using Content.Shared.Input;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._ViewportGui.ViewportUserInterface;

public interface IViewportUserInterfaceManager
{
    event Action<HUDRoot>? OnScreenLoad;
    event Action<HUDRoot>? OnScreenUnload;

    /// <summary>
    /// Contain position, size, content size and another useful info.
    /// </summary>
    ViewportDrawingInfo? DrawingInfo { get; set; }

    /// <summary>
    /// Viewport control. Should be set up by Overlay/Initialize logic.
    /// Should be used for KeyBind function for mouse buttons.
    /// </summary>
    ScalingViewport? Viewport { get; set; }

    /// <summary>
    /// Contains all HUD elements near viewport. And should do only that.
    /// </summary>
    HUDRoot? Root { get; }

    /// <summary>
    /// Can we interact with world objects on screen position.
    /// If VP-GUI element is focused - we should not do any interactions by another systems.
    /// </summary>
    bool CanMouseInteractInWorld { get; }

    void Initialize();
    void FrameUpdate(FrameEventArgs args);
    void Draw(ViewportUIDrawArgs args);

    ViewportDrawBounds? GetDrawingBounds();
    ResPath GetThemeRsiPath(string rsi);
    SpriteSpecifier GetThemeRsi(string rsi, string icon);
    Texture GetThemeTexture(string path);
    Texture? GetTexturePath(string path);
    Texture? GetTexturePath(ResPath path);
    bool TryGetControl<T>(string controlName, out T? control) where T : HUDControl;
    bool TryGetControl<T>(out T? control) where T : HUDControl;
    bool DoControlsBounds(
        HUDControl uicontrol,
        ref HUDBoundsCheckArgs boundsArgs,
        HUDKeyBindInfo? keyBindInfo,
        Vector2i mousePos,
        bool canInteract = true);
    void LoadScreen(HUDRoot root);
    void ReloadScreen(HUDRoot root);
    void UnloadScreen();
    HUDBoundsCheckArgs? TryFindHUDControl(HUDControl? root = null);
    Vector2i? ConvertGlobalToLocal(Vector2 screenPosition);

    /// <summary>
    /// Play UI click sound for buttons, like <seealso cref="IUserInterfaceManager"/>
    /// </summary>
    void PlayClickSound();

    /// <summary>
    /// Play UI hover sound for buttons, like <seealso cref="IUserInterfaceManager"/>
    /// </summary>
    void PlayHoverSound();
}

/// <summary>
/// Used for manage viewport HUD interface.
/// TODO: Need add hover supporting, like UIManager.
/// </summary>
public sealed class ViewportUserInterfaceManager : IViewportUserInterfaceManager
{
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    public event Action<HUDRoot>? OnScreenLoad;
    public event Action<HUDRoot>? OnScreenUnload;

    private List<BoundKeyFunction> _whitelistBoundKeys = new()
    {
        // Character movement
        EngineKeyFunctions.MoveUp,
        EngineKeyFunctions.MoveRight,
        EngineKeyFunctions.MoveDown,
        EngineKeyFunctions.MoveLeft,
        EngineKeyFunctions.Walk,

        // Camera?
        EngineKeyFunctions.CameraReset,
        EngineKeyFunctions.CameraRotateLeft,
        EngineKeyFunctions.CameraRotateRight,

        // Hands
        ContentKeyFunctions.SwapHands,
        ContentKeyFunctions.UseItemInHand,
        ContentKeyFunctions.AltUseItemInHand,
        ContentKeyFunctions.Arcade3,

        // Debug info
        EngineKeyFunctions.ShowDebugConsole,
        EngineKeyFunctions.ShowDebugMonitors,

        // EE Based keys
        ContentKeyFunctions.ToggleCombatMode,
        ContentKeyFunctions.LookUp,
        ContentKeyFunctions.FixedDirection,
        ContentKeyFunctions.IntentHelp,
        ContentKeyFunctions.IntentDisarm,
        ContentKeyFunctions.IntentGrab,
        ContentKeyFunctions.IntentHarm,
        ContentKeyFunctions.ToggleStanding,
        ContentKeyFunctions.Loadout0,
        ContentKeyFunctions.Loadout1,
        ContentKeyFunctions.Loadout2,
        ContentKeyFunctions.Loadout3,
        ContentKeyFunctions.Loadout4,
        ContentKeyFunctions.Loadout5,
        ContentKeyFunctions.Loadout6,
        ContentKeyFunctions.Loadout7,
        ContentKeyFunctions.Loadout8,
        ContentKeyFunctions.Loadout9,
    };

    private IAudioSource? _clickSoundSource;
    private IAudioSource? _hoverSoundSource;

    private float _interfaceGain;
    private const float ClickGain = 0.25f;

    private ScalingViewport? _oldViewport;
    private ScalingViewport? _viewport;

    public ViewportDrawingInfo? DrawingInfo { get; set; }

    public ScalingViewport? Viewport
    {
        get => _viewport;
        set
        {
            _oldViewport = _viewport;
            _viewport = value;
            ResolveKeyBinds();
        }
    }

    public HUDRoot? Root { get; private set; }

    public bool CanMouseInteractInWorld { get; private set; } = true;

    public void Initialize()
    {
        CanMouseInteractInWorld = true;

        _cfg.OnValueChanged(CCVars.InterfaceVolume, SetInterfaceVolume, true);
        SetClickSounds(_cfg.GetCVar(CCVars.UIClickSound), _cfg.GetCVar(CCVars.UIHoverSound));
    }

    public void FrameUpdate(FrameEventArgs args)
    {
        Root?.FrameUpdate(args);
    }

    public void Draw(ViewportUIDrawArgs args)
    {
        var handle = args.ScreenHandle;

        args.ScreenHandle.RenderInRenderTarget(args.RenderTexture, () =>
        {
            Root?.Draw(args);

            // Debug bounds drawing
            //handle.DrawRect(new UIBox2(new Vector2(0, 0), args.ContentSize), Color.Green.WithAlpha(0.5f), true);
        }, Color.Transparent);
    }

    public ViewportDrawBounds? GetDrawingBounds()
    {
        if (_viewport is null || DrawingInfo is null)
            return null;

        var drawBox = _viewport.GetDrawBox();
        var drawBoxHeight = (drawBox.Bottom - drawBox.Top) + 0.0f;
        var drawBoxScale = drawBoxHeight / (DrawingInfo.Value.ViewportSize.Y * EyeManager.PixelsPerMeter);

        var boundPositionTopLeft = new Vector2(
            drawBox.Left + ((DrawingInfo.Value.ViewportPosition.X * EyeManager.PixelsPerMeter) * drawBoxScale),
            drawBox.Top + ((DrawingInfo.Value.ViewportPosition.Y * EyeManager.PixelsPerMeter) * drawBoxScale));
        var boundPositionBottomRight = new Vector2(
            boundPositionTopLeft.X + (DrawingInfo.Value.ContentSize.X * drawBoxScale),
            boundPositionTopLeft.Y + (DrawingInfo.Value.ContentSize.Y * drawBoxScale));

        var boundsSize = new UIBox2(boundPositionTopLeft, boundPositionBottomRight);

        return new ViewportDrawBounds(boundsSize, drawBoxScale);
    }

    public ResPath GetThemeRsiPath(string rsi) =>
        new ResPath(_uiManager.CurrentTheme.Path.ToString() + rsi);

    public SpriteSpecifier GetThemeRsi(string rsi, string icon)
    {
        SpriteSpecifier sprite = new SpriteSpecifier.Rsi(GetThemeRsiPath(rsi), icon);
        return sprite;
    }

    public Texture GetThemeTexture(string path)
    {
        return _uiManager.CurrentTheme.ResolveTexture(path);
    }

    public Texture? GetTexturePath(string path)
    {
        return _resourceCache.GetTexture(path);
    }

    public Texture? GetTexturePath(ResPath path)
    {
        return _resourceCache.GetTexture(path);
    }

    public bool TryGetControl<T>(string controlName, out T? control) where T : HUDControl
    {
        control = null;
        if (Root is null)
            return false;

        control = Root.Children.OfType<T>().FirstOrDefault(c => c.Name == controlName);
        return control != null;
    }

    public bool TryGetControl<T>(out T? control) where T : HUDControl
    {
        control = null;
        if (Root is null)
            return false;

        control = Root.Children.OfType<T>().FirstOrDefault();
        return control != null;
    }

    private void SetInterfaceVolume(float obj)
    {
        _interfaceGain = obj;

        if (_clickSoundSource != null)
        {
            _clickSoundSource.Gain = ClickGain * _interfaceGain;
        }

        if (_hoverSoundSource != null)
        {
            _hoverSoundSource.Gain = ClickGain * _interfaceGain;
        }
    }

    private void SetClickSounds(string clickSoundFile, string hoverSoundFile)
    {
        if (!string.IsNullOrEmpty(clickSoundFile) &&
            !string.IsNullOrEmpty(hoverSoundFile))
        {
            var resourceClickSound = _resourceCache.GetResource<AudioResource>(clickSoundFile);
            var resourceHoverSound = _resourceCache.GetResource<AudioResource>(hoverSoundFile);

            var sourceClickSound =
                _audioManager.CreateAudioSource(resourceClickSound);
            var sourceHoverSound =
                _audioManager.CreateAudioSource(resourceHoverSound);

            if (sourceClickSound != null)
            {
                sourceClickSound.Gain = ClickGain * _interfaceGain;
                sourceClickSound.Global = true;
            }
            if (sourceHoverSound != null)
            {
                sourceHoverSound.Gain = ClickGain * _interfaceGain;
                sourceHoverSound.Global = true;
            }

            _clickSoundSource = sourceClickSound;
            _hoverSoundSource = sourceHoverSound;
        }
        else
        {
            _clickSoundSource = null;
            _hoverSoundSource = null;
        }
    }

    public void PlayClickSound()
    {
        _clickSoundSource?.Restart();
    }

    public void PlayHoverSound()
    {
        _hoverSoundSource?.Restart();
    }

    public void LoadScreen(HUDRoot root)
    {
        Root = root;
        DrawingInfo = root.DrawingInfo;

        OnScreenLoad?.Invoke(root);
    }

    public void ReloadScreen(HUDRoot root)
    {
        UnloadScreen();
        LoadScreen(root);
    }

    public void UnloadScreen()
    {
        if (Root is null)
            return;

        OnScreenUnload?.Invoke(Root);

        Root?.Dispose();
        Root = null;

        DrawingInfo = null;
    }

    private void OnKeyBindDown(GUIBoundKeyEventArgs args)
    {
        var keyBindInfo = new HUDKeyBindInfo(HUDKeyBindType.Down, args);

        if (DoInteraction(keyBindInfo) && !_whitelistBoundKeys.Contains(args.Function))
            args.Handle();
    }

    private void OnKeyBindUp(GUIBoundKeyEventArgs args)
    {
        var keyBindInfo = new HUDKeyBindInfo(HUDKeyBindType.Up, args);

        if (DoInteraction(keyBindInfo) && !_whitelistBoundKeys.Contains(args.Function))
            args.Handle();
    }

    private void ResolveKeyBinds()
    {
        if (_oldViewport is not null)
        {
            _oldViewport.OnKeyBindDown -= OnKeyBindDown;
            _oldViewport.OnKeyBindUp -= OnKeyBindUp;
        }

        if (_viewport is null)
            return;

        _viewport.OnKeyBindDown += OnKeyBindDown;
        _viewport.OnKeyBindUp += OnKeyBindUp;
    }

    private bool DoInteraction(HUDKeyBindInfo keyBindInfo)
    {
        var boundsArgs = TryFindHUDControlInternal(keyBindInfo);
        if (boundsArgs is null)
            return false;

        return boundsArgs.Value.InBounds;
    }

    public HUDBoundsCheckArgs? TryFindHUDControl(HUDControl? root = null)
    {
        if (root is null)
            root = Root;
        if (root is null)
            return null;

        var mouseScreenPos = _inputManager.MouseScreenPosition;
        var localMousePos = ConvertGlobalToLocal(mouseScreenPos.Position);
        if (localMousePos is null)
            return null;

        var boundsArgs = new HUDBoundsCheckArgs();
        var result = DoControlsBounds(root, ref boundsArgs, null, (Vector2i) localMousePos, canInteract: false);

        return boundsArgs;
    }

    // Because need apply some specific logic for only VPGui
    private HUDBoundsCheckArgs? TryFindHUDControlInternal(HUDKeyBindInfo keyBindInfo)
    {
        if (Root is null)
            return null;

        var mouseScreenPos = _inputManager.MouseScreenPosition;
        var localMousePos = ConvertGlobalToLocal(mouseScreenPos.Position);
        if (localMousePos is null)
            return null;

        var boundsArgs = new HUDBoundsCheckArgs();
        var result = DoControlsBounds(Root, ref boundsArgs, keyBindInfo, (Vector2i) localMousePos);

        return boundsArgs;
    }

    public Vector2i? ConvertGlobalToLocal(Vector2 screenPosition)
    {
        var drawBounds = GetDrawingBounds();
        if (drawBounds is null)
            return null;
        if (DrawingInfo is null)
            return null;

        var scaleX = drawBounds.Value.DrawBox.Width / DrawingInfo.Value.ContentSize.X;
        var scaleY = drawBounds.Value.DrawBox.Height / DrawingInfo.Value.ContentSize.Y;

        var localMousePosX = screenPosition.X - drawBounds.Value.DrawBox.Left;
        var localMousePosY = screenPosition.Y - drawBounds.Value.DrawBox.Bottom;

        var vpMouseX = localMousePosX / scaleX;
        var vpMouseY = localMousePosY / scaleY;

        // Because minus
        vpMouseY = vpMouseY * (-1);
        // Because mouse think - bottom is start, not top
        vpMouseY = DrawingInfo.Value.ContentSize.Y - vpMouseY;

        return new Vector2i((int) vpMouseX, (int) vpMouseY);
    }

    /// <summary>
    /// Do any logic with controls.
    /// </summary>
    /// <param name="uicontrol"></param>
    /// <param name="keyBindInfo"></param>
    /// <param name="mousePos"></param>
    /// <returns>Does mouse cursor is focused on control</returns>
    public bool DoControlsBounds(
        HUDControl uicontrol,
        ref HUDBoundsCheckArgs boundsArgs,
        HUDKeyBindInfo? keyBindInfo,
        Vector2i mousePos,
        bool canInteract = true)
    {
        // Check if the mouse is within the bounds of the current control
        if (InControlBounds(uicontrol.GlobalPosition, uicontrol.Size, mousePos))
        {
            if (uicontrol.MouseFilter >= HUDMouseFilterMode.Pass && uicontrol.VisibleInTree)
            {
                if (TryControlInteraction(uicontrol, ref boundsArgs, keyBindInfo, canInteract))
                    return true;
            }

            // Well... By some reasons we can't interact with panels
            if (!uicontrol.IgnoreBounds && uicontrol.VisibleInTree)
                boundsArgs.InBounds = true;
        }

        // Reverse recursive traversal: iterate through children in reverse order
        var childsList = uicontrol.Children.ToArray();
        for (int i = uicontrol.ChildCount - 1; i >= 0; i--)
        {
            var control = childsList[i];
            if (DoControlsBounds(control, ref boundsArgs, keyBindInfo, mousePos))
                return true;
        }

        return boundsArgs.IsFocused;
    }

    private bool TryControlInteraction(
        HUDControl uicontrol,
        ref HUDBoundsCheckArgs boundsArgs,
        HUDKeyBindInfo? keyBindInfo,
        bool canInteract = true)
    {
        // Handle key bind events
        if (canInteract && keyBindInfo is not null)
        {
            if (keyBindInfo.Value.KeyBindType == HUDKeyBindType.Down)
                uicontrol.KeyBindDown(keyBindInfo.Value.KeyEventArgs);
            else if (keyBindInfo.Value.KeyBindType == HUDKeyBindType.Up)
                uicontrol.KeyBindUp(keyBindInfo.Value.KeyEventArgs);
        }

        // Update bounds arguments
        if (!uicontrol.IgnoreBounds)
            boundsArgs.InBounds = true;
        boundsArgs.FocusedControl = uicontrol;
        boundsArgs.IsFocused = true;

        // If the control stops further interaction, return true
        if (uicontrol.MouseFilter == HUDMouseFilterMode.Stop)
            return true;

        return false;
    }

    private bool InControlBounds(Vector2i controlPos, Vector2i controlSize, Vector2i pos)
    {
        var bounds = new UIBox2i(controlPos, controlPos + controlSize);
        var mouseBounds = new UIBox2i(pos, pos);

        return bounds.Intersects(mouseBounds);
    }
}

public enum HUDKeyBindType
{
    Down = 0,
    Up = 1
}

public record struct HUDBoundsCheckArgs
{
    public HUDBoundsCheckArgs()
    {
    }

    public HUDControl? FocusedControl { get; set; }

    public bool InBounds { get; set; } = false;
    public bool IsFocused { get; set; } = false;
}

public struct HUDKeyBindInfo
{
    public readonly HUDKeyBindType KeyBindType { get; }
    public readonly GUIBoundKeyEventArgs KeyEventArgs { get; }

    public HUDKeyBindInfo(HUDKeyBindType keyBindType, GUIBoundKeyEventArgs keyEventArgs)
    {
        KeyBindType = keyBindType;
        KeyEventArgs = keyEventArgs;
    }
}

public struct ViewportDrawBounds
{
    public UIBox2 DrawBox { get; set; }
    public float Scale { get; set; }

    public ViewportDrawBounds(UIBox2 drawBox, float scale)
    {
        DrawBox = drawBox;
        Scale = scale;
    }
}

public struct ViewportDrawingInfo
{
    public Vector2i ViewportPosition { get; set; }
    public Vector2i ViewportSize { get; set; }
    public Vector2i ContentSize { get; set; }
    public Vector2i OffsetSize { get; set; }
    public Vector2i OffsetPosition { get; set; }
    public ViewportDrawingInfo(
        Vector2i viewportPosition,
        Vector2i viewportSize,
        Vector2i contentSize,
        Vector2i offsetSize,
        Vector2i offsetPosition)
    {
        ViewportPosition = viewportPosition;
        ViewportSize = viewportSize;
        ContentSize = contentSize;
        OffsetSize = offsetSize;
        OffsetPosition = offsetPosition;
    }
}
