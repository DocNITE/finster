using Robust.Shared.GameStates;

namespace Content.Shared.Area;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AreaComponent : Component
{
    /// <summary>
    /// ID of id - is a list of reserved tiles
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, AreaData> Data = new();

    [DataField, AutoNetworkedField]
    public Color Color { get; set; } = Color.Red;
}

[Serializable]
public sealed class AreaData
{
    [DataField]
    public List<Vector2i> Tiles { get; set; } = new();

    [DataField]
    public Color Color { get; set; } = Color.Red;
}
