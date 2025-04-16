using Robust.Shared.GameStates;

namespace Content.Shared.MiddleClick;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MiddleClickIntentComponent : Component
{
    /// <summary>
    ///     Click type, raised by MMB.
    ///     If it is generic - it should use actions based on normal intents or systems.
    /// </summary>
    [DataField, AutoNetworkedField]
    public MiddleClickType ClickType = MiddleClickType.Generic;
}

public enum MiddleClickType : byte
{
    /// <summary>
    ///     Do not do any middle click. Do another action based on hands and generic intents.
    /// </summary>
    Generic,

    /// <summary>
    ///     TODO: Need make system or find another parts where character can be knocked by velocity.
    ///     Kick the character do adjust the distance ot kick him for falling down on multi-Z.
    /// </summary>
    Kick,

    /// <summary>
    ///     Climbing on walls and tiles.
    /// </summary>
    Climb,

    /// <summary>
    ///     Jumping on tiles....
    /// </summary>
    Jump
}
