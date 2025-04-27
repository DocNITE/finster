using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Finster.Rulebook;


/// <summary>
/// No, it's not like SS14's DiceSystem. It is internal system for another systems calculation.
///
/// NOTES: We should use 1d20 for attack checking. 2d4, 2d6, 2d8 and etc. for skills check.
/// </summary>
public sealed class DiceSystem : EntitySystem
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
    /// Roll dice and accept your fate...
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
    /// Roll generic attack, with generic result checking.
    /// </summary>
    /// <param name="attackerModifier">Attacker's roll modifier</param>
    /// <param name="targetModifier">Target's roll modifier</param>
    /// <param name="attackerDice">Dice type (by default 1d20)</param>
    /// <param name="targetDice">Dice type (by default 1d20)</param>
    /// <param name="attackerDicesCount">How many dices should be roled side by side</param>
    /// <param name="targetDicesCount">How many dices should be roled side by side</param>
    /// <returns>Result for attacking - sucessful or not</returns>
    public bool RollAttack(
            out CriticalType attackerCriticalResult,
            out CriticalType targetCriticalResult,
            int attackerModifier = 0,
            int targetModifier = 0,
            Dice attackerDice = Dice.D20,
            Dice targetDice = Dice.D20,
            int attackerDicesCount = 1,
            int targetDicesCount = 1)
    {
        var attackerResult = Roll(attackerDice, out var attackerCritical, modifier: attackerModifier, count: attackerDicesCount);
        var targetResult = Roll(targetDice, out var targetCritical, modifier: targetModifier, count: targetDicesCount);

        // Re-roll if result is same
        if (targetResult == attackerResult)
        {
            var result = RollAttack(
                    out var atCrit,
                    out var tarCrit,
                    attackerModifier,
                    targetModifier,
                    attackerDice,
                    targetDice,
                    attackerDicesCount,
                    targetDicesCount);
            attackerCriticalResult = atCrit;
            targetCriticalResult = tarCrit;
            return result;
        }

        attackerCriticalResult = attackerCritical;
        targetCriticalResult = targetCritical;

        // If roll from attacker is critical successful - then attack is successful
        // If critical failure - missed
        if (attackerCritical == CriticalType.Success)
        {
            return true;
        }
        else if (attackerCritical == CriticalType.Failure ||
            targetCritical == CriticalType.Success)
        {
            return false;
        }

        // Check rolls.
        if (attackerResult > targetResult)
            return true;
        else
            return false;
    }

    /// <summary>
    /// Roll generic skill result, as using 2d(N) dice.
    /// </summary>
    /// <param name="criticalType"></param>
    /// <param name="modifier"></param>
    /// <param name="dice"></param>
    /// <param name="difficulty"></param>
    /// <returns></returns>
    public bool RollSkill(out CriticalType criticalType, int modifier = 0, Dice dice = Dice.D6, int difficulty = 6)
    {
        var result = Roll(dice, out var critical, modifier: modifier, count: 2);

        criticalType = critical;

        if (critical == CriticalType.Failure ||
            critical != CriticalType.Success && result < difficulty)
            return false;
        else
            return true;
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
