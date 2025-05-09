using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._ViewportGui.ViewportUserInterface.UI;

public class HUDTextureButton : HUDButton
{
    /// <summary>
    /// Drawing texture. For setting size or position use <seealso cref="HUDTextureRect"/>'s Size and Position variables.
    /// </summary>
    public Texture? Texture { get; set; }

    public override void Draw(in ViewportUIDrawArgs args)
    {
        var handle = args.ScreenHandle;

        if (Texture is null || !Visible)
        {
            base.Draw(args);
            return;
        }

        handle.DrawTextureRect(Texture, new UIBox2(GlobalPosition, GlobalPosition + Size));
        base.Draw(args);
    }
}

