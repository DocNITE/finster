using Content.Server.Chat.Systems;
using Content.Server.CombatMode.Disarm;
using Content.Server.Movement.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Actions.Events;
using Content.Shared.Administration.Components;
using Content.Shared.Contests;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Effects;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared._White.Intent;
using Content.Shared._White.Intent.Event;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;
using Content.Shared.Chat;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Damage.Components;
using Content.Shared._Finster.Rulebook;
using Robust.Shared.Prototypes;
using Content.Shared.CombatMode;

namespace Content.Server.Weapons.Melee;

public sealed class MeleeWeaponSystem : SharedMeleeWeaponSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageExamineSystem _damageExamine = default!;
    [Dependency] private readonly LagCompensationSystem _lag = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly ContestsSystem _contests = default!;
    [Dependency] private readonly PullingSystem _pulling = default!; // WD EDIT
    [Dependency] private readonly SharedTargetingSystem _targeting = default!; // WWDP
    [Dependency] private readonly DiceSystem _dice = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedIntentSystem _intent = default!; // WD EDIT

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeSpeechComponent, MeleeHitEvent>(OnSpeechHit);
        SubscribeLocalEvent<MeleeWeaponComponent, DamageExamineEvent>(OnMeleeExamineDamage, after: [typeof(GunSystem)]);

        SubscribeAllEvent<HeavyAttackEvent>(OnHeavyAttack);
        SubscribeAllEvent<LightAttackEvent>(OnLightAttack);
        SubscribeAllEvent<DisarmAttackEvent>(OnDisarmAttack);
        SubscribeAllEvent<GrabAttackEvent>(OnGrabAttack);
        SubscribeAllEvent<StopAttackEvent>(OnStopAttack);
    }

    private void OnMeleeExamineDamage(EntityUid uid, MeleeWeaponComponent component, ref DamageExamineEvent args)
    {
        if (component.Hidden)
            return;

        var damageSpec = GetDamage(uid, args.User, component);
        if (damageSpec.Empty)
            return;

        if (!component.DisableClick)
            _damageExamine.AddDamageExamine(args.Message, damageSpec, Loc.GetString("damage-melee"));

        if (!component.DisableHeavy)
        {
            if (damageSpec * component.HeavyDamageBaseModifier != damageSpec)
                _damageExamine.AddDamageExamine(args.Message, damageSpec * component.HeavyDamageBaseModifier, Loc.GetString("damage-melee-heavy"));

            if (component.HeavyStaminaCost != 0)
            {
                var staminaCostMarkup = FormattedMessage.FromMarkupOrThrow(
                    Loc.GetString("damage-stamina-cost",
                    ("type", Loc.GetString("damage-melee-heavy")), ("cost", Math.Round(component.HeavyStaminaCost, 2).ToString("0.##"))));
                args.Message.PushNewline();
                args.Message.AddMessage(staminaCostMarkup);
            }
        }
    }

    protected override bool ArcRaySuccessful(EntityUid targetUid, Vector2 position, Angle angle, Angle arcWidth, float range, MapId mapId,
        EntityUid ignore, ICommonSession? session)
    {
        // Originally the client didn't predict damage effects so you'd intuit some level of how far
        // in the future you'd need to predict, but then there was a lot of complaining like "why would you add artifical delay" as if ping is a choice.
        // Now damage effects are predicted but for wide attacks it differs significantly from client and server so your game could be lying to you on hits.
        // This isn't fair in the slightest because it makes ping a huge advantage and this would be a hidden system.
        // Now the client tells us what they hit and we validate if it's plausible.

        // Even if the client is sending entities they shouldn't be able to hit:
        // A) Wide-damage is split anyway
        // B) We run the same validation we do for click attacks.

        // Could also check the arc though future effort + if they're aimbotting it's not really going to make a difference.

        // (This runs lagcomp internally and is what clickattacks use)
        if (!Interaction.InRangeUnobstructed(ignore, targetUid, range + 0.1f))
            return false;

        // TODO: Check arc though due to the aforementioned aimbot + damage split comments it's less important.
        return true;
    }

    private void OnStopAttack(StopAttackEvent msg, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.AttachedEntity;

        if (user == null)
            return;

        if (!TryGetWeapon(user.Value, out var weaponUid, out var weapon) ||
            weaponUid != GetEntity(msg.Weapon))
        {
            return;
        }

        if (!weapon.Attacking)
            return;

        weapon.Attacking = false;
        DirtyField(weaponUid, weapon, nameof(MeleeWeaponComponent.Attacking));
    }

    private void OnLightAttack(LightAttackEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (!TryGetWeapon(user, out var weaponUid, out var weapon) ||
            weaponUid != GetEntity(msg.Weapon))
        {
            return;
        }

        AttemptAttack(user, weaponUid, weapon, msg, args.SenderSession);
    }

    private void OnHeavyAttack(HeavyAttackEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (!TryGetWeapon(user, out var weaponUid, out var weapon) ||
            weaponUid != GetEntity(msg.Weapon) ||
            !weapon.CanWideSwing) // Goobstation Change
        {
            return;
        }

        AttemptAttack(user, weaponUid, weapon, msg, args.SenderSession);
    }

    private void OnDisarmAttack(DisarmAttackEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (TryGetWeapon(user, out var weaponUid, out var weapon))
            AttemptAttack(user, weaponUid, weapon, msg, args.SenderSession);
    }

    private void OnGrabAttack(GrabAttackEvent msg, EntitySessionEventArgs args) // WD EDIT
    {
        if (args.SenderSession.AttachedEntity == null)
            return;

        if (!TryGetWeapon(args.SenderSession.AttachedEntity.Value, out var weaponUid, out var weapon))
            return;

        AttemptAttack(args.SenderSession.AttachedEntity.Value, weaponUid, weapon, msg, args.SenderSession);
    }

    protected override bool DoDisarm(EntityUid user, DisarmAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        if (!base.DoDisarm(user, ev, meleeUid, component, session))
            return false;

        if (!TryComp<IntentComponent>(user, out var intent) || !intent.CanDisarm) // WD EDIT
            return false;

        var target = GetEntity(ev.Target!.Value);

        if (_mobState.IsIncapacitated(target))
        {
            return false;
        }

        if (!TryComp<HandsComponent>(target, out var targetHandsComponent))
        {
            if (!TryComp<StatusEffectsComponent>(target, out var status) || !status.AllowedEffects.Contains("KnockedDown"))
                return false;
        }

        if (!InRange(user, target, component.Range, session))
        {
            return false;
        }

        EntityUid? inTargetHand = null;

        if (targetHandsComponent?.ActiveHand is { IsEmpty: false })
        {
            inTargetHand = targetHandsComponent.ActiveHand.HeldEntity!.Value;
        }

        Interaction.DoContactInteraction(user, target);

        var attemptEvent = new DisarmAttemptEvent(target, user, inTargetHand);

        if (inTargetHand != null)
        {
            RaiseLocalEvent(inTargetHand.Value, attemptEvent);
        }

        RaiseLocalEvent(target, attemptEvent);

        if (attemptEvent.Cancelled)
            return false;

        var chance = CalculateDisarmChance(user, target, inTargetHand, intent);
        if (!_random.Prob(chance))
        {
            // Don't play a sound as the swing is already predicted.
            // Also don't play popups because most disarms will miss.
            return false;
        }

        var filterOther = Filter.PvsExcept(user, entityManager: EntityManager);
        var msgPrefix = "disarm-action-";

        if (inTargetHand == null)
            msgPrefix = "disarm-action-shove-";

        var msgOther = Loc.GetString(
                msgPrefix + "popup-message-other-clients",
                ("performerName", Identity.Entity(user, EntityManager)),
                ("targetName", Identity.Entity(target, EntityManager)));

        var msgUser = Loc.GetString(msgPrefix + "popup-message-cursor", ("targetName", Identity.Entity(target, EntityManager)));

        PopupSystem.PopupEntity(msgOther, user, filterOther, true);
        PopupSystem.PopupEntity(msgUser, target, user);

        _audio.PlayPvs(intent.DisarmSuccessSound, user, AudioParams.Default.WithVariation(0.025f).WithVolume(5f));
        AdminLogger.Add(LogType.DisarmedAction, $"{ToPrettyString(user):user} used disarm on {ToPrettyString(target):target}");

        var staminaDamage = (TryComp<ShovingComponent>(user, out var shoving) ? shoving.StaminaDamage : ShovingComponent.DefaultStaminaDamage)
            * Math.Clamp(chance, 0f, 1f);

        var eventArgs = new DisarmedEvent { Target = target, Source = user, PushProbability = chance, StaminaDamage = staminaDamage };
        RaiseLocalEvent(target, eventArgs);

        if (!eventArgs.Handled)
            return false;

        _audio.PlayPvs(intent.DisarmSuccessSound, user, AudioParams.Default.WithVariation(0.025f).WithVolume(5f));
        AdminLogger.Add(LogType.DisarmedAction, $"{ToPrettyString(user):user} used disarm on {ToPrettyString(target):target}");

        return true;
    }

    protected override bool DoGrab(EntityUid user, GrabAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        if (!base.DoGrab(user, ev, meleeUid, component, session))
            return false;

        if (!TryComp<IntentComponent>(user, out var intent) || !intent.CanGrab) // WD EDIT
            return false;

        var target = GetEntity(ev.Target!.Value);

        if (_mobState.IsIncapacitated(target))
            return false;

        if (!HasComp<HandsComponent>(target))
            return false;

        if (!InRange(user, target, component.Range, session))
            return false;

        _pulling.TryStartPull(user, target);
        _pulling.TryGrab(target, user, isServerOnlySoundEffect: true);

        return true;
    }

    protected override bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session)
    {
        EntityCoordinates targetCoordinates;
        Angle targetLocalAngle;

        if (session is { } pSession)
        {
            (targetCoordinates, targetLocalAngle) = _lag.GetCoordinatesAngle(target, pSession);
            return Interaction.InRangeUnobstructed(user, target, targetCoordinates, targetLocalAngle, range);
        }

        return Interaction.InRangeUnobstructed(user, target, range);
    }

    protected override void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform)
    {
        var filter = Filter.Pvs(targetXform.Coordinates, entityMan: EntityManager);
        _color.RaiseEffect(Color.Red, targets, filter);
    }

    private float CalculateDisarmChance(EntityUid disarmer, EntityUid disarmed, EntityUid? inTargetHand, IntentComponent disarmerComp)
    {
        if (HasComp<DisarmProneComponent>(disarmer))
            return 1.0f;

        if (HasComp<DisarmProneComponent>(disarmed))
            return 0.0f;

        var chance = 1 - disarmerComp.BaseDisarmFailChance;

        if (inTargetHand != null && TryComp<DisarmMalusComponent>(inTargetHand, out var malus))
            chance -= malus.Malus;

        if (TryComp<ShovingComponent>(disarmer, out var shoving))
            chance += shoving.DisarmBonus;

        return Math.Clamp(chance
                        * _contests.MassContest(disarmer, disarmed, false, 2f)
                        * _contests.StaminaContest(disarmer, disarmed, false, 0.5f)
                        * _contests.HealthContest(disarmer, disarmed, false, 1f),
                        0f, 1f);
    }

    public override void DoLunge(EntityUid user, EntityUid weapon, Angle angle, Vector2 localPos, string? animation, bool predicted = true)
    {
        Filter filter = Filter.Pvs(user, entityManager: EntityManager);
        RaiseNetworkEvent(new MeleeLungeEvent(GetNetEntity(user), GetNetEntity(weapon), angle, localPos, animation), filter);
    }

    private void OnSpeechHit(EntityUid owner, MeleeSpeechComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit ||
        !args.HitEntities.Any())
        {
            return;
        }

        if (comp.Battlecry != null)//If the battlecry is set to empty, doesn't speak
        {
            _chat.TrySendInGameICMessage(args.User, comp.Battlecry, InGameICChatType.Speak, true, true, checkRadioPrefix: false);  //Speech that isn't sent to chat or adminlogs
        }

    }

    public void AttemptLightAttackMiss(
            EntityUid user,
            EntityUid weaponUid,
            MeleeWeaponComponent weapon,
            EntityCoordinates coordinates,
            bool ignoreCooldown = false)
    {
        AttemptAttack(user, weaponUid, weapon, new LightAttackEvent(null, GetNetEntity(weaponUid), GetNetCoordinates(coordinates), ignoreCooldown), null);
    }

    public bool AttemptLightAttack(
            EntityUid user,
            EntityUid weaponUid,
            MeleeWeaponComponent weapon,
            EntityUid target,
            bool ignoreCooldown = false)
    {
        if (!TryComp(target, out TransformComponent? targetXform))
            return false;

        return AttemptAttack(user, weaponUid, weapon, new LightAttackEvent(GetNetEntity(target), GetNetEntity(weaponUid), GetNetCoordinates(targetXform.Coordinates), ignoreCooldown), null);
    }

    public void AttemptHeavyAttackMiss(
            EntityUid user,
            EntityUid weaponUid,
            MeleeWeaponComponent weapon,
            EntityCoordinates coordinates,
            bool ignoreCooldown = false)
    {
        AttemptAttack(user, weaponUid, weapon, new HeavyAttackEvent(GetNetEntity(weaponUid), new List<NetEntity>(), GetNetCoordinates(coordinates), ignoreCooldown), null);
    }

    public bool AttemptHeavyAttack(
            EntityUid user,
            EntityUid weaponUid,
            MeleeWeaponComponent weapon,
            List<NetEntity> targets,
            EntityCoordinates coordinates,
            bool ignoreCooldown = false)
    {
        return AttemptAttack(user, weaponUid, weapon, new HeavyAttackEvent(GetNetEntity(weaponUid), targets, GetNetCoordinates(coordinates), ignoreCooldown), null);
    }

    public bool AttemptDisarmAttack(EntityUid user, EntityUid weaponUid, MeleeWeaponComponent weapon, EntityUid target)
    {
        if (!TryComp(target, out TransformComponent? targetXform))
            return false;

        return AttemptAttack(user, weaponUid, weapon, new DisarmAttackEvent(GetNetEntity(target), GetNetCoordinates(targetXform.Coordinates)), null);
    }

    /// <summary>
    /// Called when a windup is finished and an attack is tried.
    /// </summary>
    /// <returns>True if attack successful</returns>
    private bool AttemptAttack(
            EntityUid user,
            EntityUid weaponUid,
            MeleeWeaponComponent weapon,
            AttackEvent attack,
            ICommonSession? session)
    {
        var curTime = Timing.CurTime;

        if (!attack.IgnoreCooldown && weapon.NextAttack > curTime)
            return false;

        if (!_intent.CanAttack(user)) // WD EDIT
            return false;

        var fireRateSwingModifier = 1f;

        EntityUid? target = null;
        switch (attack)
        {
            case LightAttackEvent light:
                if (light.Target != null && !TryGetEntity(light.Target, out target))
                {
                    // Target was lightly attacked & deleted.
                    return false;
                }

                if (!Blocker.CanAttack(user, target, (weaponUid, weapon)))
                    return false;

                // Can't self-attack if you're the weapon
                // DocNight's note - no, i CAN. Why i can't to punch myself like Taino Dernul from Fight club?
                //if (weaponUid == target)
                //    return false;

                break;
            case HeavyAttackEvent heavy:
                // Can't use heavy AOE attack by hands.
                // But we can do it with martial arts!
                if (weaponUid == user)
                {
                    if (_dice.TryGetSkill(user, SkillMartialArts, out var skillLevel))
                        break;
                    else
                        return false;
                }

                fireRateSwingModifier = weapon.HeavyRateModifier;
                break;
            case DisarmAttackEvent disarm:
                if (disarm.Target != null && !TryGetEntity(disarm.Target, out target))
                {
                    // Target was lightly attacked & deleted.
                    return false;
                }

                if (!Blocker.CanAttack(user, target, (weaponUid, weapon), true))
                    return false;
                break;
            case GrabAttackEvent grab:
                var grabTarget = GetEntity(grab.Target);

                if (!Blocker.CanAttack(user, grabTarget, (weaponUid, weapon), true))
                    return false;
                break;
            default:
                if (!Blocker.CanAttack(user, weapon: (weaponUid, weapon)))
                    return false;
                break;
        }

        // Windup time checked elsewhere.
        var fireRate = TimeSpan.FromSeconds(GetAttackRate(weaponUid, user, weapon) * fireRateSwingModifier);
        var swings = 0;

        // TODO: If we get autoattacks then probably need a shotcounter like guns so we can do timing properly.
        if (weapon.NextAttack < curTime)
            weapon.NextAttack = curTime;

        // It needs for grab cooldown
        PullerComponent? pullerComp = null;
        if (TryComp<PullerComponent>(user, out var pullerUserComp))
            pullerComp = pullerUserComp;

        while (weapon.NextAttack <= curTime)
        {
            // FIXME: Oh hell no, hardcoded grab cooldown :woozy_face:
            if (pullerComp is not null && attack as GrabAttackEvent is not null)
                weapon.NextAttack += pullerComp.StageChangeCooldown;
            else
                weapon.NextAttack += fireRate;
            swings++;
        }

        DirtyField(weaponUid, weapon, nameof(MeleeWeaponComponent.NextAttack));

        // Do this AFTER attack so it doesn't spam every tick
        var ev = new AttemptMeleeEvent
        {
            PlayerUid = user
        };
        RaiseLocalEvent(weaponUid, ref ev);

        if (ev.Cancelled)
        {
            if (ev.Message != null)
            {
                PopupSystem.PopupClient(ev.Message, weaponUid, user);
            }

            return false;
        }

        // Attack confirmed
        for (var i = 0; i < swings; i++)
        {
            string animation;

            switch (attack)
            {
                case LightAttackEvent light:
                    DoLightAttack(user, light, weaponUid, weapon, session);
                    animation = weapon.Animation;
                    break;
                case DisarmAttackEvent disarm:
                    if (!DoDisarm(user, disarm, weaponUid, weapon, session))
                        return false;

                    animation = weapon.Animation;
                    break;
                case GrabAttackEvent grab:
                    if (!DoGrab(user, grab, weaponUid, weapon, session))
                        return false;

                    animation = weapon.Animation;
                    break;
                case HeavyAttackEvent heavy:
                    if (!DoHeavyAttack(user, heavy, weaponUid, weapon, session))
                        return false;

                    animation = weapon.WideAnimation;
                    break;
                default:
                    throw new NotImplementedException();
            }

            DoLungeAnimation(user, weaponUid, weapon.Angle, TransformSystem.ToMapCoordinates(GetCoordinates(attack.Coordinates)), weapon.Range, animation);
        }

        var attackEv = new MeleeAttackEvent(weaponUid);
        RaiseLocalEvent(user, ref attackEv);

        weapon.Attacking = true;
        return true;
    }

    private void DoLungeAnimation(EntityUid user, EntityUid weapon, Angle angle, MapCoordinates coordinates, float length, string? animation)
    {
        // TODO: Assert that offset eyes are still okay.
        if (!TryComp(user, out TransformComponent? userXform))
            return;

        var invMatrix = TransformSystem.GetInvWorldMatrix(userXform);
        var localPos = Vector2.Transform(coordinates.Position, invMatrix);

        if (localPos.LengthSquared() <= 0f)
            return;

        localPos = userXform.LocalRotation.RotateVec(localPos);

        // We'll play the effect just short visually so it doesn't look like we should be hitting but actually aren't.
        const float bufferLength = 0.2f;
        var visualLength = length - bufferLength;

        if (localPos.Length() > visualLength)
            localPos = localPos.Normalized() * visualLength;

        DoLunge(user, weapon, angle, localPos, animation);
    }

    private void MissLightAttack(EntityUid user, EntityUid meleeUid, MeleeWeaponComponent component, DamageSpecifier damage)
    {
        // Leave IsHit set to true, because the only time it's set to false
        // is when a melee weapon is examined. Misses are inferred from an
        // empty HitEntities.
        // TODO: This needs fixing
        if (meleeUid == user)
        {
            AdminLogger.Add(LogType.MeleeHit,
                LogImpact.Low,
                $"{ToPrettyString(user):actor} melee attacked (light) using their hands and missed");
        }
        else
        {
            AdminLogger.Add(LogType.MeleeHit,
                LogImpact.Low,
                $"{ToPrettyString(user):actor} melee attacked (light) using {ToPrettyString(meleeUid):tool} and missed");
        }
        var missEvent = new MeleeHitEvent(new List<EntityUid>(), user, meleeUid, damage, null);
        RaiseLocalEvent(meleeUid, missEvent);
        MeleeSound.PlaySwingSound(user, meleeUid, component);
    }

    private bool TryDodgeAttack(EntityUid user, EntityUid? target, EntityUid weapon, TargetBodyPart targetPart)
    {
        if (Deleted(target))
            return false;

        var ev = new AttemptDodgeMeleeAttack(user, target.Value, weapon, targetPart);
        RaiseLocalEvent(target.Value, ref ev);
        return ev.Handled;
    }

    private bool TryParryAttack(EntityUid user, EntityUid? target, EntityUid weapon, TargetBodyPart targetPart)
    {
        if (Deleted(target))
            return false;

        var ev = new AttemptParryMeleeAttack(user, target.Value, weapon, targetPart);
        RaiseLocalEvent(target.Value, ref ev);
        return ev.Handled;
    }

    private void DoLightAttack(EntityUid user, LightAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        // If I do not come back later to fix Light Attacks being Heavy Attacks you can throw me in the spider pit -Errant
        var damage = GetDamage(meleeUid, user, component) * GetHeavyDamageModifier(meleeUid, user, component);
        var target = GetEntity(ev.Target);
        var weapon = GetEntity(ev.Weapon);
        var resistanceBypass = GetResistanceBypass(meleeUid, user, component);

        // WWDP edit; bodypart targeting
        TargetBodyPart targetPart;
        if (TryComp<TargetingComponent>(user, out var targeting))
            targetPart = targeting.Target;
        else
            targetPart = _targeting.GetRandomBodyPart();

        // If dual mode is enabled - try use second hand
        // TODO: Need make all modifiers for generic fights.
        // And then we can add dual mode for weapons
        //if (TryComp<CombatModeComponent>(user, out var combatMode) &&
        //    combatMode.Style == CombatIntent.Dual)

        // For consistency with wide attacks stuff needs damageable.
        if (!CanDoLightAttack(user, target, component, out var targetXform, session) ||
            TryDodgeAttack(user, target, weapon, targetPart))
        {
            MissLightAttack(user, meleeUid, component, damage);
            return;
        }

        var targets = new List<EntityUid>(1)
        {
            target.Value
        };

        // Raise event before doing damage so we can cancel damage if the event is handled
        var hitEvent = new MeleeHitEvent(targets, user, meleeUid, damage, null);
        RaiseLocalEvent(meleeUid, hitEvent);

        if (hitEvent.Handled)
            return;

        // We skip weapon -> target interaction, as forensics system applies DNA on hit
        Interaction.DoContactInteraction(user, weapon);

        // If the user is using a long-range weapon, this probably shouldn't be happening? But I'll interpret melee as a
        // somewhat messy scuffle. See also, heavy attacks.
        Interaction.DoContactInteraction(user, target);

        // Target can attempt to parry the attack, with using his skills and dice roll.
        if (TryParryAttack(user, target, weapon, targetPart))
        {
            MeleeSound.PlayParrySound(target.Value, weapon, component);
            return;
        }

        // For stuff that cares about it being attacked.
        var attackedEvent = new AttackedEvent(meleeUid, user, targetXform.Coordinates, targetPart);
        RaiseLocalEvent(target.Value, attackedEvent);

        var modifiedDamage = DamageSpecifier.ApplyModifierSets(damage + hitEvent.BonusDamage + attackedEvent.BonusDamage, hitEvent.ModifiersList);
        var damageResult = Damageable.TryChangeDamage(target, modifiedDamage, origin: user, ignoreResistances: resistanceBypass, partMultiplier: component.ClickPartDamageMultiplier, targetPart: targetPart);
        // WWDP edit end
        //var comboEv = new ComboAttackPerformedEvent(user, target.Value, meleeUid, ComboAttackType.Harm);
        //RaiseLocalEvent(user, comboEv);

        if (damageResult is { Empty: false })
        {
            // If the target has stamina and is taking blunt damage, they should also take stamina damage based on their blunt to stamina factor
            if (damageResult.DamageDict.TryGetValue("Blunt", out var bluntDamage))
            {
                _stamina.TakeStaminaDamage(target.Value, (bluntDamage * component.BluntStaminaDamageFactor).Float(), visual: false, source: user, with: meleeUid == user ? null : meleeUid);
            }

            if (meleeUid == user)
            {
                AdminLogger.Add(LogType.MeleeHit,
                    LogImpact.Medium,
                    $"{ToPrettyString(user):actor} melee attacked (light) {ToPrettyString(target.Value):subject} using their hands and dealt {damageResult.GetTotal():damage} damage");
            }
            else
            {
                AdminLogger.Add(LogType.MeleeHit,
                    LogImpact.Medium,
                    $"{ToPrettyString(user):actor} melee attacked (light) {ToPrettyString(target.Value):subject} using {ToPrettyString(meleeUid):tool} and dealt {damageResult.GetTotal():damage} damage");
            }

        }

        MeleeSound.PlayHitSound(target.Value, user, GetHighestDamageSound(modifiedDamage, _protoManager), hitEvent.HitSoundOverride, component.SoundHit, component.SoundNoDamage);

        if (damageResult?.GetTotal() > FixedPoint2.Zero)
        {
            DoDamageEffect(targets, user, targetXform);
        }
    }

    private void MissHeavyAttack(EntityUid user, EntityUid meleeUid, MeleeWeaponComponent component, DamageSpecifier damage, Vector2 direction)
    {
        if (meleeUid == user)
        {
            AdminLogger.Add(LogType.MeleeHit,
                LogImpact.Low,
                $"{ToPrettyString(user):actor} melee attacked (heavy) using their hands and missed");
        }
        else
        {
            AdminLogger.Add(LogType.MeleeHit,
                LogImpact.Low,
                $"{ToPrettyString(user):actor} melee attacked (heavy) using {ToPrettyString(meleeUid):tool} and missed");
        }
        var missEvent = new MeleeHitEvent(new List<EntityUid>(), user, meleeUid, damage, direction);
        RaiseLocalEvent(meleeUid, missEvent);

        // immediate audio feedback
        MeleeSound.PlaySwingSound(user, meleeUid, component);
    }

    private bool DoHeavyAttack(EntityUid user, HeavyAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        // TODO: This is copy-paste as fuck with DoPreciseAttack
        if (!TryComp(user, out TransformComponent? userXform))
            return false;

        var targetMap = TransformSystem.ToMapCoordinates(GetCoordinates(ev.Coordinates));

        if (targetMap.MapId != userXform.MapID)
            return false;

        if (TryComp<StaminaComponent>(user, out var stamina))
        {
            if (stamina.CritThreshold - stamina.StaminaDamage <= component.HeavyStaminaCost)
            {
                PopupSystem.PopupClient(Loc.GetString("melee-heavy-no-stamina"), meleeUid, user);
                return false;
            }

            _stamina.TakeStaminaDamage(user, component.HeavyStaminaCost, stamina, visual: false);
        }

        var userPos = TransformSystem.GetWorldPosition(userXform);
        var direction = targetMap.Position - userPos;
        var distance = Math.Min(component.Range, direction.Length());

        var damage = GetDamage(meleeUid, user, component);
        var entities = GetEntityList(ev.Entities);

        // Naughty input
        if (entities.Count > component.MaxTargets)
        {
            entities.RemoveRange(component.MaxTargets, entities.Count - component.MaxTargets);
        }

        // Validate client
        for (var i = entities.Count - 1; i >= 0; i--)
        {
            if (ArcRaySuccessful(entities[i],
                    userPos,
                    direction.ToWorldAngle(),
                    component.Angle,
                    distance,
                    userXform.MapID,
                    user,
                    session))
            {
                continue;
            }

            // Bad input
            entities.RemoveAt(i);
        }

        var targets = new List<EntityUid>();
        var damageQuery = GetEntityQuery<DamageableComponent>();

        // WWDP edit; bodypart targeting
        TargetBodyPart targetPart;
        if (TryComp<TargetingComponent>(user, out var targeting))
            targetPart = targeting.Target;
        else
            targetPart = _targeting.GetRandomBodyPart();

        foreach (var entity in entities)
        {
            if (entity == user ||
                !damageQuery.HasComponent(entity) || // Can't deal damage to undamageable
                TryDodgeAttack(user, entity, meleeUid, targetPart)) // If dodged, then dodged
                continue;

            targets.Add(entity);
        }

        // If there is no any targets
        if (entities.Count == 0 || targets.Count == 0)
        {
            MissHeavyAttack(user, meleeUid, component, damage, direction);
            return true;
        }

        // Raise event before doing damage so we can cancel damage if the event is handled
        var hitEvent = new MeleeHitEvent(targets, user, meleeUid, damage, direction);
        RaiseLocalEvent(meleeUid, hitEvent);

        if (hitEvent.Handled)
            return true;

        var weapon = GetEntity(ev.Weapon);

        Interaction.DoContactInteraction(user, weapon);

        // For stuff that cares about it being attacked.
        foreach (var target in targets)
        {
            // We skip weapon -> target interaction, as forensics system applies DNA on hit

            // If the user is using a long-range weapon, this probably shouldn't be happening? But I'll interpret melee as a
            // somewhat messy scuffle. See also, light attacks.
            Interaction.DoContactInteraction(user, target);
        }

        var appliedDamage = new DamageSpecifier();

        for (var i = targets.Count - 1; i >= 0; i--)
        {
            var entity = targets[i];
            // We raise an attack attempt here as well,
            // primarily because this was an untargeted wideswing: if a subscriber to that event cared about
            // the potential target (such as for pacifism), they need to be made aware of the target here.
            // In that case, just continue.
            if (!Blocker.CanAttack(user, entity, (weapon, component)))
            {
                targets.RemoveAt(i);
                continue;
            }

            // Target can attempt to parry the attack, with using his skills and dice roll.
            if (TryParryAttack(user, entity, weapon, targetPart))
            {
                MeleeSound.PlayParrySound(entity, weapon, component);
                continue;
            }

            var attackedEvent = new AttackedEvent(meleeUid, user, GetCoordinates(ev.Coordinates), targetPart);
            RaiseLocalEvent(entity, attackedEvent);

            var modifiedDamage = DamageSpecifier.ApplyModifierSets(damage + hitEvent.BonusDamage + attackedEvent.BonusDamage, hitEvent.ModifiersList);

            var damageResult = Damageable.TryChangeDamage(entity, modifiedDamage, origin: user, partMultiplier: component.HeavyPartDamageMultiplier, targetPart: targetPart);
            // WWDP edit end
            //var comboEv = new ComboAttackPerformedEvent(user, entity, meleeUid, ComboAttackType.HarmLight);
            //RaiseLocalEvent(user, comboEv);

            if (damageResult != null && damageResult.GetTotal() > FixedPoint2.Zero)
            {
                // If the target has stamina and is taking blunt damage, they should also take stamina damage based on their blunt to stamina factor
                if (damageResult.DamageDict.TryGetValue("Blunt", out var bluntDamage))
                {
                    _stamina.TakeStaminaDamage(entity, (bluntDamage * component.BluntStaminaDamageFactor).Float(), visual: false, source: user, with: meleeUid == user ? null : meleeUid);
                }

                appliedDamage += damageResult;

                if (meleeUid == user)
                {
                    AdminLogger.Add(LogType.MeleeHit,
                        LogImpact.Medium,
                        $"{ToPrettyString(user):actor} melee attacked (heavy) {ToPrettyString(entity):subject} using their hands and dealt {damageResult.GetTotal():damage} damage");
                }
                else
                {
                    AdminLogger.Add(LogType.MeleeHit,
                        LogImpact.Medium,
                        $"{ToPrettyString(user):actor} melee attacked (heavy) {ToPrettyString(entity):subject} using {ToPrettyString(meleeUid):tool} and dealt {damageResult.GetTotal():damage} damage");
                }
            }
        }

        if (entities.Count != 0)
        {
            var target = entities.First();
            MeleeSound.PlayHitSound(target, user, GetHighestDamageSound(appliedDamage, _protoManager), hitEvent.HitSoundOverride, component.SoundHit, component.SoundNoDamage);
        }

        if (appliedDamage.GetTotal() > FixedPoint2.Zero)
        {
            DoDamageEffect(targets, user, Transform(targets[0]));
        }

        return true;
    }
}
