using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._Shitmed.Targeting; // Goobstation - Martial Arts
using Content.Shared._White;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared._White.Intent;
using Content.Shared._White.Intent.Event;
using Content.Shared.Contests;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using ItemToggleMeleeWeaponComponent = Content.Shared.Item.ItemToggle.Components.ItemToggleMeleeWeaponComponent;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared._Finster.Rulebook;
using Robust.Shared.Network;

namespace Content.Shared.Weapons.Melee;

public abstract class SharedMeleeWeaponSystem : EntitySystem
{
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] protected readonly SharedInteractionSystem Interaction = default!;
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] protected readonly SharedPopupSystem PopupSystem = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] protected readonly MeleeSoundSystem MeleeSound = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ContestsSystem _contests = default!;

    public const string SkillMartialArts = "MartialArts";

    private const int AttackMask = (int) (CollisionGroup.MobMask | CollisionGroup.Opaque);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MeleeWeaponComponent, HandSelectedEvent>(OnMeleeSelected);
        SubscribeLocalEvent<BonusMeleeDamageComponent, GetMeleeDamageEvent>(OnGetBonusMeleeDamage);
        SubscribeLocalEvent<BonusMeleeDamageComponent, GetHeavyDamageModifierEvent>(OnGetBonusHeavyDamageModifier);
        SubscribeLocalEvent<BonusMeleeAttackRateComponent, GetMeleeAttackRateEvent>(OnGetBonusMeleeAttackRate);

        SubscribeLocalEvent<ItemToggleMeleeWeaponComponent, ItemToggledEvent>(OnItemToggle);

#if DEBUG
        SubscribeLocalEvent<MeleeWeaponComponent,
                            MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, MeleeWeaponComponent component, MapInitEvent args)
    {
        if (component.NextAttack > Timing.CurTime)
            Log.Warning($"Initializing a map that contains an entity that is on cooldown. Entity: {ToPrettyString(uid)}");
#endif
    }

    private void OnMeleeSelected(EntityUid uid, MeleeWeaponComponent component, HandSelectedEvent args)
    {
        var attackRate = GetAttackRate(uid, args.User, component);
        if (attackRate.Equals(0f))
            return;

        if (!component.ResetOnHandSelected)
            return;

        if (Paused(uid))
            return;

        // If someone swaps to this weapon then reset its cd.
        var curTime = Timing.CurTime;
        var minimum = curTime + TimeSpan.FromSeconds(attackRate);

        if (minimum < component.NextAttack)
            return;

        component.NextAttack = minimum;
        DirtyField(uid, component, nameof(MeleeWeaponComponent.NextAttack));
    }

    private void OnGetBonusMeleeDamage(EntityUid uid, BonusMeleeDamageComponent component, ref GetMeleeDamageEvent args)
    {
        if (component.BonusDamage != null)
            args.Damage += component.BonusDamage;
        if (component.DamageModifierSet != null)
            args.Modifiers.Add(component.DamageModifierSet);
    }

    private void OnGetBonusHeavyDamageModifier(EntityUid uid, BonusMeleeDamageComponent component, ref GetHeavyDamageModifierEvent args)
    {
        args.DamageModifier += component.HeavyDamageFlatModifier;
        args.Multipliers *= component.HeavyDamageMultiplier;
    }

    private void OnGetBonusMeleeAttackRate(EntityUid uid, BonusMeleeAttackRateComponent component, ref GetMeleeAttackRateEvent args)
    {
        args.Rate += component.FlatModifier;
        args.Multipliers *= component.Multiplier;
    }

    /// <summary>
    /// Gets the total damage a weapon does, including modifiers like wielding and enablind/disabling
    /// </summary>
    public DamageSpecifier GetDamage(EntityUid uid, EntityUid user, MeleeWeaponComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return new DamageSpecifier();

        var ev = new GetMeleeDamageEvent(uid, new(component.Damage), new(), user, component.ResistanceBypass);
        RaiseLocalEvent(uid, ref ev);

        if (component.ContestArgs is not null)

            ev.Damage *= _contests.ContestConstructor(user, component.ContestArgs);

        return DamageSpecifier.ApplyModifierSets(ev.Damage, ev.Modifiers);
    }

    public float GetAttackRate(EntityUid uid, EntityUid user, MeleeWeaponComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return 0;

        var ev = new GetMeleeAttackRateEvent(uid, component.AttackRate, 1, user);
        RaiseLocalEvent(uid, ref ev);

        return ev.Rate * ev.Multipliers;
    }

    public FixedPoint2 GetHeavyDamageModifier(EntityUid uid, EntityUid user, MeleeWeaponComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return FixedPoint2.Zero;

        var ev = new GetHeavyDamageModifierEvent(uid, component.ClickDamageModifier, 1, user);
        RaiseLocalEvent(uid, ref ev);

        return ev.DamageModifier * ev.Multipliers * component.HeavyDamageBaseModifier;
    }

    public bool GetResistanceBypass(EntityUid uid, EntityUid user, MeleeWeaponComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var ev = new GetMeleeDamageEvent(uid, new(component.Damage), new(), user, component.ResistanceBypass);
        RaiseLocalEvent(uid, ref ev);

        return ev.ResistanceBypass;
    }

    public bool TryGetWeapon(EntityUid entity, out EntityUid weaponUid, [NotNullWhen(true)] out MeleeWeaponComponent? melee)
    {
        weaponUid = default;
        melee = null;

        var ev = new GetMeleeWeaponEvent();
        RaiseLocalEvent(entity, ev);
        if (ev.Handled)
        {
            if (TryComp(ev.Weapon, out melee))
            {
                weaponUid = ev.Weapon.Value;
                return true;
            }

            return false;
        }

        // Use inhands entity if we got one.
        if (EntityManager.TryGetComponent(entity, out HandsComponent? hands) &&
            hands.ActiveHandEntity is { } held)
        {
            // Make sure the entity is a weapon AND it doesn't need
            // to be equipped to be used (E.g boxing gloves).
            if (EntityManager.TryGetComponent(held, out melee) &&
                !melee.MustBeEquippedToUse)
            {
                weaponUid = held;
                return true;
            }

            if (!HasComp<VirtualItemComponent>(held))
                return false;
        }

        // Use hands clothing if applicable.
        if (_inventory.TryGetSlotEntity(entity, "gloves", out var gloves) &&
            TryComp<MeleeWeaponComponent>(gloves, out var glovesMelee))
        {
            weaponUid = gloves.Value;
            melee = glovesMelee;
            return true;
        }

        // Use our own melee
        if (TryComp(entity, out melee))
        {
            weaponUid = entity;
            return true;
        }

        return false;
    }

    protected abstract bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session);

    protected bool CanDoLightAttack(EntityUid user, [NotNullWhen(true)] EntityUid? target, MeleeWeaponComponent component, [NotNullWhen(true)] out TransformComponent? targetXform, ICommonSession? session = null)
    {
        targetXform = null;
        return !Deleted(target) &&
            HasComp<DamageableComponent>(target) &&
            TryComp<TransformComponent>(target, out targetXform) &&
            // Not in LOS.
            InRange(user, target.Value, component.Range, session);
    }

    protected abstract void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform);

    protected HashSet<EntityUid> ArcRayCast(Vector2 position, Angle angle, Angle arcWidth, float range, MapId mapId, EntityUid ignore)
    {
        // TODO: This is pretty sucky.
        var widthRad = arcWidth;
        var increments = 1 + 35 * (int) Math.Ceiling(widthRad / (2 * Math.PI));
        var increment = widthRad / increments;
        var baseAngle = angle - widthRad / 2;

        var resSet = new HashSet<EntityUid>();

        for (var i = 0; i < increments; i++)
        {
            var castAngle = new Angle(baseAngle + increment * i);
            var res = _physics.IntersectRay(mapId,
                new CollisionRay(position,
                    castAngle.ToWorldVec(),
                    AttackMask),
                range,
                ignore,
                false)
                .ToList();

            if (res.Count != 0)
            {
                resSet.Add(res[0].HitEntity);
            }
        }

        return resSet;
    }

    protected virtual bool ArcRaySuccessful(EntityUid targetUid,
        Vector2 position,
        Angle angle,
        Angle arcWidth,
        float range,
        MapId mapId,
        EntityUid ignore,
        ICommonSession? session)
    {
        // Only matters for server.
        return true;
    }


    public static string? GetHighestDamageSound(DamageSpecifier modifiedDamage, IPrototypeManager protoManager)
    {
        var groups = modifiedDamage.GetDamagePerGroup(protoManager);

        // Use group if it's exclusive, otherwise fall back to type.
        if (groups.Count == 1)
        {
            return groups.Keys.First();
        }

        var highestDamage = FixedPoint2.Zero;
        string? highestDamageType = null;

        foreach (var (type, damage) in modifiedDamage.DamageDict)
        {
            if (damage <= highestDamage)
                continue;

            highestDamageType = type;
        }

        return highestDamageType;
    }

    protected virtual bool DoDisarm(EntityUid user, DisarmAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        var target = GetEntity(ev.Target);

        if (Deleted(target) ||
            user == target)
        {
            return false;
        }

        // Play a sound to give instant feedback; same with playing the animations
        MeleeSound.PlaySwingSound(user, meleeUid, component);
        return true;
    }

    protected virtual bool DoGrab(EntityUid user, GrabAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        var target = GetEntity(ev.Target);

        if (Deleted(target) || user == target)
            return false;

        // Play a sound to give instant feedback; same with playing the animations
        MeleeSound.PlaySwingSound(user, meleeUid, component);
        return true;
    }

    public abstract void DoLunge(EntityUid user, EntityUid weapon, Angle angle, Vector2 localPos, string? animation, bool predicted = true);

    /// <summary>
    /// Used to update the MeleeWeapon component on item toggle.
    /// </summary>
    private void OnItemToggle(EntityUid uid, ItemToggleMeleeWeaponComponent itemToggleMelee, ItemToggledEvent args)
    {
        if (!TryComp(uid, out MeleeWeaponComponent? meleeWeapon))
            return;

        if (args.Activated)
        {
            if (itemToggleMelee.ActivatedDamage != null)
            {
                //Setting deactivated damage to the weapon's regular value before changing it.
                itemToggleMelee.DeactivatedDamage ??= meleeWeapon.Damage;
                meleeWeapon.Damage = itemToggleMelee.ActivatedDamage;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.Damage));
            }

            meleeWeapon.SoundHit = itemToggleMelee.ActivatedSoundOnHit;

            if (itemToggleMelee.ActivatedSoundOnHitNoDamage != null)
            {
                //Setting the deactivated sound on no damage hit to the weapon's regular value before changing it.
                itemToggleMelee.DeactivatedSoundOnHitNoDamage ??= meleeWeapon.SoundNoDamage;
                meleeWeapon.SoundNoDamage = itemToggleMelee.ActivatedSoundOnHitNoDamage;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.SoundNoDamage));
            }

            if (itemToggleMelee.ActivatedSoundOnSwing != null)
            {
                //Setting the deactivated sound on no damage hit to the weapon's regular value before changing it.
                itemToggleMelee.DeactivatedSoundOnSwing ??= meleeWeapon.SoundSwing;
                meleeWeapon.SoundSwing = itemToggleMelee.ActivatedSoundOnSwing;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.SoundSwing));
            }

            if (itemToggleMelee.DeactivatedSecret)
            {
                meleeWeapon.Hidden = false;
            }
        }
        else
        {
            if (itemToggleMelee.DeactivatedDamage != null)
            {
                meleeWeapon.Damage = itemToggleMelee.DeactivatedDamage;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.Damage));
            }

            meleeWeapon.SoundHit = itemToggleMelee.DeactivatedSoundOnHit;
            DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.SoundHit));

            if (itemToggleMelee.DeactivatedSoundOnHitNoDamage != null)
            {
                meleeWeapon.SoundNoDamage = itemToggleMelee.DeactivatedSoundOnHitNoDamage;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.SoundNoDamage));
            }

            if (itemToggleMelee.DeactivatedSoundOnSwing != null)
            {
                meleeWeapon.SoundSwing = itemToggleMelee.DeactivatedSoundOnSwing;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.SoundSwing));
            }

            if (itemToggleMelee.DeactivatedSecret)
            {
                meleeWeapon.Hidden = true;
            }
        }
    }
}
