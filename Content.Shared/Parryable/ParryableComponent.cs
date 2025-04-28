using Robust.Shared.GameStates;

namespace Content.Shared._Finster.Rulebook;

/// <summary>
/// All attributes from the character for using role/dice systems.
/// It also can be applied for the another entity, if we wanna use RolePlay mechanics on them.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ParryableComponent : Component
{
    /// <summary>
    /// Should it require active hand.
    /// By default it's true, because when we using the 1H weapon we should
    /// parry with that weapon.
    ///
    /// Instead, if we have shield in some hand, we can ignore if statement for
    /// active hand.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequiredActiveHand = true;
}
