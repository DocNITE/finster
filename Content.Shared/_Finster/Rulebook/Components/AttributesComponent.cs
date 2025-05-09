using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Finster.Rulebook;

/// <summary>
/// All attributes from the character for using role/dice systems.
/// It also can be applied for the another entity, if we wanna use RolePlay mechanics on them.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AttributesComponent : Component
{
    // Basic points for all attributes
    public const int BaseStatsPoint = 10;

    [DataField, AutoNetworkedField]
    public Dictionary<Attributes, int> Stats { get; set; } = new()
    {
        { Attributes.Strength, BaseStatsPoint },
        { Attributes.Dexterity, BaseStatsPoint },
        { Attributes.Intelligence, BaseStatsPoint },
        { Attributes.Endurance, BaseStatsPoint },
        { Attributes.Reflex, BaseStatsPoint },
        { Attributes.Willpower, BaseStatsPoint }
    };

    /// <summary>
    /// Contains ID of prototypes of buffs and debuffs on specific attributes.
    /// But it is only specified for attributes. In most scenarios you should use
    /// <seealso cref="StatusEffectPrototype"/>
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
    public List<string> Effects = new();

    /// <summary>
    /// Calculate modifier, given by the attribute.
    /// </summary>
    /// <param name="attributeValue">Attribute?</param>
    /// <returns></returns>
    public static int GetModifier(int attributeValue)
    {
        return (attributeValue - BaseStatsPoint) / 2;
    }
}

/*
EXAMPLE OF ADDING ATTRS:
- type: entity
  id: ExampleEntity
  components:
  - type: Attributes
    stats:
      Strength: 15  # Overrides default 10
      Dexterity: 12 # Overrides default 8
      Intelligence: 14 # Adds new entry
*/

[Serializable, NetSerializable]
public enum Attributes : byte
{
    /// <summary>
    ///     It can help you to deal more damage. Also it help to control fire from
    ///     ranged weapon, and another calculations.
    /// </summary>
    Strength,

    /// <summary>
    ///     Dodge attacks from the enemies! Affect on acrobatic movement.
    /// </summary>
    Dexterity,

    /// <summary>
    ///     Affect on communication skills, understading and learning something new.
    /// </summary>
    Intelligence,

    /// <summary>
    ///     Reduce any damage, like physical. It also can reduce time in crit, when your death is become.
    /// </summary>
    Endurance,

    /// <summary>
    ///     Can help to aim with ranged weapons and detected some trap.
    ///     Affect on melee combat.
    /// </summary>
    Reflex,

    /// <summary>
    ///     It helps you to live. Reduce negative effects for mood and increase time for crit, before you can die.
    /// </summary>
    Willpower,

    // Not attribute
    Max
}
