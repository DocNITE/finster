using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Finster.Rulebook;


/// <summary>
/// No, it's not like SS14's DiceSystem. It is internal system for another systems calculation.
///
/// NOTES: We should use 1d20 for attack checking. 2d4, 2d6, 2d8 and etc. for skills check.
/// </summary>
public sealed class RolePlayDiceSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private EntityQuery<SkillsComponent> _skillsQuery;
    private EntityQuery<AttributesComponent> _attributesQuery;

    public override void Initialize()
    {
        base.Initialize();

        _skillsQuery = GetEntityQuery<SkillsComponent>();
        _attributesQuery = GetEntityQuery<AttributesComponent>();
    }

    /// <summary>
    /// Roll dice and try your luck!
    /// </summary>
    /// <param name="diceType">The dice what should be thrown.</param>
    /// <param name="modifier">Apply modifiers.</param>
    /// <param name="critical"></param>
    /// <returns></returns>
    public int Roll(
            Dice diceType,
            out CriticalType critical,
            int count = 1,
            int modifier = 0)
    {
        critical = CriticalType.None;
        if (count <= 0) return 0; // защита от невалидного количества костей

        var sides = (int) diceType;
        int totalResult = 0;
        bool hasCriticalSuccess = false;
        bool hasCriticalFailure = false;

        for (int i = 0; i < count; i++)
        {
            var result = _random.Next(1, sides + 1);
            totalResult += result;

            if (result == sides)
                hasCriticalSuccess = true;
            else if (result == 1)
                hasCriticalFailure = true;
        }

        if (hasCriticalSuccess && hasCriticalFailure)
            critical = CriticalType.None;
        else if (hasCriticalSuccess)
            critical = CriticalType.Success;
        else if (hasCriticalFailure)
            critical = CriticalType.Failure;

        return totalResult + modifier;
    }

    /// <summary>
    /// Get correct attribute poins, with buffs and debuffs.
    /// </summary>
    /// <param name="targetAttribute"></param>
    /// <returns></returns>
    public bool TryGetAttributePoints(
        EntityUid uid,
        Attributes targetAttribute,
        out int points,
        AttributesComponent? comp = null,
        bool ignoreEffects = false)
    {
        points = -1;

        if (!Resolve(uid, ref comp))
            return false;

        // Set the basic stat poins.
        points = comp.Stats[targetAttribute];
        if (ignoreEffects)
            return true;

        // TODO: Effects - buffs or debuffs

        return true;
    }

    public bool TryGetSkill(EntityUid uid, string id, out SkillLevel level)
    {
        // Only as basic. We should overwrite it by component.
        level = SkillLevel.Weak;

        if (!_skillsQuery.TryComp(uid, out var skills) ||
            !_protoManager.TryIndex<SkillPrototype>(id, out var skill))
            return false;

        if (skills.Stats.TryGetValue(skill, out var skillLevel))
        {
            level = skillLevel;
            return true;
        }

        /*
        foreach (var skillEntry in skills.Skills)
        {
            if (skillEntry.Key.Id == skill.ID)
            {
                level = skillEntry.Value;
                return true;
            }
        }
        */

        return false;
    }
}

public enum CriticalType
{
    None,
    Success,
    Failure
}

/// <summary>
/// Holy...
/// </summary>
public enum Dice
{
    D4 = 4,
    D6 = 6,
    D8 = 8,
    D10 = 10,
    D12 = 12,
    D20 = 20,
    D100 = 100
}
