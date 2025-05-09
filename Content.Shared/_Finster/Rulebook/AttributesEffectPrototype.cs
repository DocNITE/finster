using Robust.Shared.Prototypes;

namespace Content.Shared._Finster.Rulebook;

[Prototype]
public sealed partial class AttributesEffectPrototype : IPrototype
{
    /// <summary>
    ///     The ID of the moodlet to use.
    /// </summary>
    [IdDataField]
    public string ID { get; } = default!;
    public string Description => Loc.GetString($"attributes-effect-{ID}");

    /// <summary>
    ///     How much we should affect on attributes.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<Attributes, int> Changes { get; set; } = new()
    {
        { Attributes.Strength, 0 },
        { Attributes.Dexterity, 0 },
        { Attributes.Intelligence, 0 },
        { Attributes.Endurance, 0 },
        { Attributes.Reflex, 0 },
        { Attributes.Willpower, 0 }
    };

    /// <summary>
    ///     How long, this effect will affect? If is null - then timeout is not exist.
    /// </summary>
    [DataField]
    public int? Timeout;

    /// <summary>
    ///     When not null, this moodlet will replace itself with another Moodlet upon expiration.
    /// </summary>
    [DataField]
    public ProtoId<AttributesEffectPrototype>? AttributeLetOnEnd;
}
