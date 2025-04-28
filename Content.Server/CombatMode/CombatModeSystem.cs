using Content.Server._White.Notice;
using Content.Server.NPC.HTN;
using Content.Shared._Finster.Rulebook;
using Content.Shared.Alert;
using Content.Shared.CombatMode;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Server.CombatMode;

public sealed class CombatModeSystem : SharedCombatModeSystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly NoticeSystem _notice = default!;
    [Dependency] private readonly DiceSystem _dice = default!;

    private readonly ProtoId<AlertPrototype> _defenseModeAlert = "DefenseMode";
    private readonly ProtoId<AlertPrototype> _combatIntentAlert = "CombatIntent";
    private readonly ProtoId<AlertCategoryPrototype> _combatIntentCategory = "CombatIntent";

    // Styles
    private readonly ProtoId<AlertPrototype> _combatStyleClose = "CombatStyleClose";
    private readonly ProtoId<AlertPrototype> _combatStyleWeak = "CombatStyleWeak";
    private readonly ProtoId<AlertPrototype> _combatStyleAimed = "CombatStyleAimed";
    private readonly ProtoId<AlertPrototype> _combatStyleFurious = "CombatStyleFurious";
    private readonly ProtoId<AlertPrototype> _combatStyleStrong = "CombatStyleStrong";
    private readonly ProtoId<AlertPrototype> _combatStyleDefend = "CombatStyleDefend";
    private readonly ProtoId<AlertPrototype> _combatStyleDual = "CombatStyleDual";
    private readonly ProtoId<AlertPrototype> _combatStyleFeint = "CombatStyleFeint";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CombatModeComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<CombatModeComponent, AttemptDodgeMeleeAttack>(OnDodgeAttempt);
        SubscribeLocalEvent<CombatModeComponent, AttemptParryMeleeAttack>(OnParryAttempt);

        SubscribeNetworkEvent<ToggleCombatModeEvent>(OnToggleCombatMode);

    }

    private void OnInit(EntityUid uid, CombatModeComponent component, ComponentStartup args)
    {
        RefreshAlert(uid, component);
        RefreshIntentsAlerts(uid, component);
    }

    private void OnDodgeAttempt(EntityUid uid, CombatModeComponent component, ref AttemptDodgeMeleeAttack args)
    {
        // If target is not prepared for combat - ignore dodging
        if (!component.IsInCombatMode ||
            component.DefenseStyle != DefenseMode.Dodge)
            return;

        int targetModifier = 0;
        int attackerModifier = 0;

        // Apply dexterity modifier for target
        if (TryComp<AttributesComponent>(args.Target, out var targetAttributes) &&
            _dice.TryGetAttributePoints(args.Target, Attributes.Dexterity, out var targetDexPoints, targetAttributes))
            targetModifier += AttributesComponent.GetModifier(targetDexPoints);

        // Apply dexterity modifier for attacker
        if (TryComp<AttributesComponent>(args.Attacker, out var attackerAttributes) &&
            _dice.TryGetAttributePoints(args.Attacker, Attributes.Dexterity, out var attackerDexPoints, attackerAttributes))
            attackerModifier += AttributesComponent.GetModifier(attackerDexPoints);

        // Roll
        args.Handled = !_dice.RollAttack(out var _, out var _, attackerModifier, targetModifier);
    }

    private void OnParryAttempt(EntityUid uid, CombatModeComponent component, ref AttemptParryMeleeAttack args)
    {
        // If target is not prepared for combat - ignore dodging
        if (!component.IsInCombatMode ||
            component.DefenseStyle != DefenseMode.Parry)
            return;

        // Can't parry bare hands
        if (args.Weapon == args.Attacker)
            return;

        // TODO: Weapon group checking, because we can't fight with soda can.

        int targetModifier = 0;
        int attackerModifier = 0;

        // TODO: Aimed, fury, strong... I mean, need implement combat intents into the game lol.
        // It is not difficult
        // TODO 2: Also, add VV variables for Attirubtes's stats for apply from View Variables
        // TODO 3: StatusEffects should affect on modifiers and attributes, instead of using attributes effect
        // TODO 4: Modifiers based on TargetBodyPart:
        // - Torso: modifier is 0
        // - Hands, Legs, Head: Modifier is -3
        // - Mouth, Eyes, Neck: Modifier is -6
        // TODO 5: Add loging into chat, based on CombatLogSystem
        // TODO 6: Make parryable component or add another variable
        // Shields should not require the active hand

        // Apply dexterity modifier for target
        if (TryComp<AttributesComponent>(args.Target, out var targetAttributes) &&
            _dice.TryGetAttributePoints(args.Target, Attributes.Dexterity, out var targetDexPoints, targetAttributes))
            targetModifier += AttributesComponent.GetModifier(targetDexPoints);

        // Apply dexterity modifier for attacker
        if (TryComp<AttributesComponent>(args.Attacker, out var attackerAttributes) &&
            _dice.TryGetAttributePoints(args.Attacker, Attributes.Dexterity, out var attackerDexPoints, attackerAttributes))
            attackerModifier += AttributesComponent.GetModifier(attackerDexPoints);

        // Roll
        args.Handled = !_dice.RollAttack(out var _, out var _, attackerModifier, targetModifier);
    }

    public override void OnShutdown(EntityUid uid, CombatModeComponent component, ComponentShutdown args)
    {
        base.OnShutdown(uid, component, args);

        _alerts.ClearAlertCategory(uid, component.CombatModeCategory);
        _alerts.ClearAlertCategory(uid, _combatIntentCategory);

        ClearStyleAlerts(uid);
    }

    public void OnToggleCombatMode(ToggleCombatModeEvent ev, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue)
            return;

        var uid = args.SenderSession.AttachedEntity.Value;
        if (!TryComp(uid, out CombatModeComponent? comp))
            return;

        ToggleCombatMode(uid, comp);
    }

    public void ToggleCombatMode(EntityUid uid, CombatModeComponent comp)
    {
        PerformAction(uid, comp, uid);
        RefreshAlert(uid, comp);

        // Send notice
        _notice.SendNoticeMessage(uid,
            comp.IsInCombatMode ? Loc.GetString("notice-combatmode-on") : Loc.GetString("notice-combatmode-off"),
            PopupType.SmallCaution);
    }

    public void ToggleDefenseMode(EntityUid uid, CombatModeComponent comp)
    {
        if (comp.DefenseStyle == DefenseMode.Parry)
            comp.DefenseStyle = DefenseMode.Dodge;
        else
            comp.DefenseStyle = DefenseMode.Parry;

        RefreshAlert(uid, comp);
    }

    public void ToggleIntentsMenu(EntityUid uid, CombatModeComponent comp)
    {
        comp.ShowCombatStyles = !comp.ShowCombatStyles;
        RefreshIntentsAlerts(uid, comp);
    }

    public void SetIntent(EntityUid uid, CombatModeComponent comp, CombatIntent intent)
    {
        comp.Style = intent;
        comp.ShowCombatStyles = false;
        RefreshIntentsAlerts(uid, comp);
    }

    public void RefreshAlert(EntityUid uid, CombatModeComponent comp)
    {
        _alerts.ShowAlert(uid, comp.CombatModeAlert, comp.IsInCombatMode ? (short) 1 : (short) 0);
        _alerts.ShowAlert(uid, _defenseModeAlert, (short) comp.DefenseStyle);
    }

    public void RefreshIntentsAlerts(EntityUid uid, CombatModeComponent comp)
    {
        _alerts.ShowAlert(uid, _combatIntentAlert, (short) comp.Style);

        if (!comp.ShowCombatStyles)
        {
            ClearStyleAlerts(uid);
            return;
        }

        ShowStyleAlerts(uid);
    }

    public void ShowStyleAlerts(EntityUid uid)
    {
        _alerts.ShowAlert(uid, _combatStyleClose);
        _alerts.ShowAlert(uid, _combatStyleWeak);
        _alerts.ShowAlert(uid, _combatStyleAimed);
        _alerts.ShowAlert(uid, _combatStyleFurious);
        _alerts.ShowAlert(uid, _combatStyleStrong);
        _alerts.ShowAlert(uid, _combatStyleDefend);
        _alerts.ShowAlert(uid, _combatStyleDual);
        _alerts.ShowAlert(uid, _combatStyleFeint);
    }

    public void ClearStyleAlerts(EntityUid uid)
    {
        _alerts.ClearAlert(uid, _combatStyleClose);
        _alerts.ClearAlert(uid, _combatStyleWeak);
        _alerts.ClearAlert(uid, _combatStyleAimed);
        _alerts.ClearAlert(uid, _combatStyleFurious);
        _alerts.ClearAlert(uid, _combatStyleStrong);
        _alerts.ClearAlert(uid, _combatStyleDefend);
        _alerts.ClearAlert(uid, _combatStyleDual);
        _alerts.ClearAlert(uid, _combatStyleFeint);
    }


    protected override bool IsNpc(EntityUid uid)
    {
        return HasComp<HTNComponent>(uid);
    }
}

// ALERT CLICKS

[UsedImplicitly, DataDefinition]
public sealed partial class ToggleCombatModeAlert : IAlertClick
{
    public void AlertClicked(EntityUid uid)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();

        if (!entityManager.TryGetComponent(uid, out CombatModeComponent? comp))
            return;

        entityManager.System<CombatModeSystem>().ToggleCombatMode(uid, comp);
    }
}

[UsedImplicitly, DataDefinition]
public sealed partial class ShowCombatIntentsMenuAlert : IAlertClick
{
    public void AlertClicked(EntityUid uid)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();

        if (!entityManager.TryGetComponent(uid, out CombatModeComponent? comp))
            return;

        entityManager.System<CombatModeSystem>().ToggleIntentsMenu(uid, comp);
    }
}

[UsedImplicitly, DataDefinition]
public sealed partial class SetCombatStyleAlert : IAlertClick
{
    [DataField("intent", required: true)]
    public CombatIntent Intent { get; set; }

    public void AlertClicked(EntityUid uid)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();

        if (!entityManager.TryGetComponent(uid, out CombatModeComponent? comp))
            return;

        entityManager.System<CombatModeSystem>().SetIntent(uid, comp, Intent);
    }
}

[UsedImplicitly, DataDefinition]
public sealed partial class ToggleDefenseModeAlert : IAlertClick
{
    public void AlertClicked(EntityUid uid)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();

        if (!entityManager.TryGetComponent(uid, out CombatModeComponent? comp))
            return;

        entityManager.System<CombatModeSystem>().ToggleDefenseMode(uid, comp);
    }
}
