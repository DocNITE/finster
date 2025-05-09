using Robust.Shared.GameStates;

namespace Content.Shared.Area;

[RegisterComponent, NetworkedComponent]
public sealed partial class SetTileAreaComponent : Component
{
    [DataField]
    public Color Color { get; set; } = Color.Red;
}
