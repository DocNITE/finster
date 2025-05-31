using System.Numerics;
using Content.Client._ViewportGui.ViewportUserInterface;
using Content.Client._ViewportGui.ViewportUserInterface.UI;
using Content.Client.UserInterface;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.UserInterface.Systems.Storage.Controls;

public class HUDItemGridControl : HUDTextureButton, IEntityControl
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] protected readonly IViewportUserInterfaceManager VPUIManager = default!;

    public static int DefaultButtonSize = 32;

    public EntityUid? Entity;
    EntityUid? IEntityControl.UiEntity => Entity;

    public Vector2i GridPosition = Vector2i.Zero;

    public HUDItemGridControl()
    {
        IoCManager.InjectDependencies(this);

        Name = Loc.GetString("slotbutton-storage-empty");
        Size = (DefaultButtonSize, DefaultButtonSize);
        CanEmitSound = false;
        Texture = VPUIManager.GetThemeTexture("Storage/slot");
    }

    public override void Draw(in ViewportUIDrawArgs args)
    {
        var handle = args.ScreenHandle;

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
}
