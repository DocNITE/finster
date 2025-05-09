using Content.Client._ViewportGui.ViewportUserInterface;
using Content.Client._ViewportGui.ViewportUserInterface.UI;
using Content.Shared._Shitmed.Targeting;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Shitmed.UserInterface.Systems.Targeting.Controls;

public class HUDTargetDoll : HUDTextureRect, IHUDDescription
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private readonly TargetingUIController _controller;

    public Dictionary<TargetBodyPart, Texture?>? BodyPartTexturesHovered;
    public Texture? Background;
    public Texture? TextureHovered;
    public Texture? TextureFocused;

    public string Description { get; set; }

    private readonly Dictionary<TargetBodyPart, HUDButton> _bodyPartControls;

    public HUDTargetDoll(TargetingUIController controller)
    {
        IoCManager.InjectDependencies(this);

        Name = "TargetDoll";
        Description = "hud-desc-targetdoll";
        Size = (32, 64);
        Position = (0, 0);
        _controller = controller;

        # region Body

        var headButton = new HUDButton()
        {
            Size = (12, 16),
            Position = (10, 3)
        };
        AddChild(headButton);

        var chestButton = new HUDButton()
        {
            Size = (14, 15),
            Position = (9, 19)
        };
        AddChild(chestButton);

        var neckButton = new HUDButton()
        {
            Size = (10, 5),
            Position = (11, 17)
        };
        AddChild(neckButton);

        var groinButton = new HUDButton()
        {
            Size = (12, 12),
            Position = (10, 31)
        };
        AddChild(groinButton);

        # endregion

        # region Hands/Arms

        var rightArmButton = new HUDButton()
        {
            Size = (7, 15),
            Position = (3, 19)
        };
        AddChild(rightArmButton);

        var rightHandButton = new HUDButton()
        {
            Size = (5, 8),
            Position = (3, 32)
        };
        AddChild(rightHandButton);

        var leftArmButton = new HUDButton()
        {
            Size = (7, 15),
            Position = (22, 19),
        };
        AddChild(leftArmButton);

        var leftHandButton = new HUDButton()
        {
            Size = (5, 8),
            Position = (24, 32)
        };
        AddChild(leftHandButton);

        # endregion

        # region Legs/Foots

        var leftLegButton = new HUDButton()
        {
            Size = (7, 19),
            Position = (17, 38)
        };
        AddChild(leftLegButton);

        var leftFootButton = new HUDButton()
        {
            Size = (8, 6),
            Position = (19, 55)
        };
        AddChild(leftFootButton);

        var rightLegButton = new HUDButton()
        {
            Size = (7, 19),
            Position = (8, 38)
        };
        AddChild(rightLegButton);

        var rightFootButton = new HUDButton()
        {
            Size = (8, 6),
            Position = (5, 55)
        };
        AddChild(rightFootButton);

        # endregion

        _bodyPartControls = new Dictionary<TargetBodyPart, HUDButton>
        {
            // TODO: ADD EYE AND MOUTH TARGETING
            { TargetBodyPart.Head, headButton },
            { TargetBodyPart.Neck, neckButton },
            { TargetBodyPart.Torso, chestButton },
            { TargetBodyPart.Groin, groinButton },
            { TargetBodyPart.LeftArm, leftArmButton },
            { TargetBodyPart.LeftHand, leftHandButton },
            { TargetBodyPart.RightArm, rightArmButton },
            { TargetBodyPart.RightHand, rightHandButton },
            { TargetBodyPart.LeftLeg, leftLegButton },
            { TargetBodyPart.LeftFoot, leftFootButton },
            { TargetBodyPart.RightLeg, rightLegButton },
            { TargetBodyPart.RightFoot, rightFootButton },
        };

        // Load hover textures
        LoadHoverTextures();

        // And set up some events on buttons
        foreach (var bodyPartButton in _bodyPartControls)
        {
            bodyPartButton.Value.MouseFilter = HUDMouseFilterMode.Stop;
            bodyPartButton.Value.OnPressed += _ => SetActiveBodyPart(bodyPartButton.Key);
            /*bodyPartButton.Value.OnMouseEntered += _ =>
            {
                if (_control.BodyPartTexturesHovered != null)
                    _control.TextureFocused = _control.BodyPartTexturesHovered[bodyPartButton.Key];
            };
            bodyPartButton.Value.OnMouseExited += _ =>
            {
                if (_control.BodyPartTexturesHovered != null)
                    _control.TextureFocused = null;
            };
            */
        }

        Background = _uiManager.CurrentTheme.ResolveTexture("TargetDoll/target_doll");
    }

    public override void Draw(in ViewportUIDrawArgs args)
    {
        var handle = args.ScreenHandle;

        if (!Visible)
            return;

        // Background
        if (Background != null)
            DrawTexture(handle, Background);

        // Draw hovered
        if (TextureHovered != null)
            DrawTexture(handle, TextureHovered);

        // Draw hovered
        if (TextureFocused != null)
            DrawTexture(handle, TextureFocused);

        base.Draw(args);
    }

    private void LoadHoverTextures()
    {
        BodyPartTexturesHovered = new Dictionary<TargetBodyPart, Texture?>();
        foreach (var item in _bodyPartControls)
        {
            string enumName = Enum.GetName(typeof(TargetBodyPart), item.Key) ?? "Unknown";
            var texture = _uiManager.CurrentTheme.ResolveTexture($"TargetDoll/target_{enumName.ToLowerInvariant()}_hover.png");
            BodyPartTexturesHovered[item.Key] = texture;
        }
    }

    public void SetBodyPartsVisible(TargetBodyPart bodyPart)
    {
        if (BodyPartTexturesHovered == null)
            return;

        foreach (var item in BodyPartTexturesHovered)
        {
            if (item.Key == bodyPart)
                TextureHovered = item.Value;
        }
    }

    private void SetActiveBodyPart(TargetBodyPart bodyPart) => _controller.CycleTarget(bodyPart);

    public void SetTargetDollVisible(bool visible) => Visible = visible;

    public void DrawTexture(DrawingHandleScreen handle, Texture tex, Color? color = null)
    {
        handle.DrawTextureRect(tex, new UIBox2(GlobalPosition, GlobalPosition + Size), color);
    }
}
