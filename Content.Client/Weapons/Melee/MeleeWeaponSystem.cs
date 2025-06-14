using System.Linq;
using Content.Client.Gameplay;
using Content.Shared.CCVar;
using Content.Shared._White.Intent;
using Content.Shared._White.Intent.Event;
using Content.Shared.Effects;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem : SharedMeleeWeaponSystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedIntentSystem _intent = default!; // WD EDIT

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<HandsComponent> _handsQuery;

    private const string MeleeLungeKey = "melee-lunge";

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _handsQuery = GetEntityQuery<HandsComponent>();
        SubscribeNetworkEvent<MeleeLungeEvent>(OnMeleeLunge);
        UpdatesOutsidePrediction = true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        UpdateEffects();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var entityNull = _player.LocalEntity;

        if (entityNull == null)
            return;

        var entity = entityNull.Value;

        if (!TryGetWeapon(entity, out var weaponUid, out var weapon))
            return;

        if (!_intent.CanAttack(entity) || !Blocker.CanAttack(entity))
        {
            weapon.Attacking = false;
            return;
        }

        var useDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use) == BoundKeyState.Down;
        var altDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary) == BoundKeyState.Down;

        // Disregard inputs to the shoot binding
        if (TryComp<GunComponent>(weaponUid, out var gun) &&
            // Except if can't shoot due to being unwielded
            (!HasComp<GunRequiresWieldComponent>(weaponUid) ||
            (TryComp<WieldableComponent>(weaponUid, out var wieldable) && wieldable.Wielded)))
        {
            if (gun.UseKey)
                useDown = false;
            else
                altDown = false;
        }

        if (weapon.AutoAttack || !useDown && !altDown)
        {
            if (weapon.Attacking)
            {
                RaiseNetworkEvent(new StopAttackEvent(GetNetEntity(weaponUid)));
            }
        }

        if (weapon.Attacking || weapon.NextAttack > Timing.CurTime || (!useDown && !altDown))
        {
            return;
        }

        // TODO using targeted actions while combat mode is enabled should NOT trigger attacks.

        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);

        if (mousePos.MapId == MapId.Nullspace)
        {
            return;
        }

        EntityCoordinates coordinates;

        if (MapManager.TryFindGridAt(mousePos, out var gridUid, out _))
        {
            coordinates = EntityCoordinates.FromMap(gridUid, mousePos, TransformSystem, EntityManager);
        }
        else
        {
            coordinates = EntityCoordinates.FromMap(MapManager.GetMapEntityId(mousePos.MapId), mousePos, TransformSystem, EntityManager);
        }

        // FINSTER EDIT - Don't attack in throw mode
        if (_handsQuery.TryComp(entity, out var handComp) && handComp.InThrowMode)
            return;
        // FINSTER EDIT END

        // Heavy attack.
        if (!weapon.DisableHeavy &&
            (!weapon.SwapKeys ? altDown : useDown) &&
            _intent.GetIntent(entity) == Intent.Harm)
        {
            ClientHeavyAttack(entity, coordinates, weaponUid, weapon);
            return;
        }

        // Light attack
        if (!weapon.DisableClick &&
            (!weapon.SwapKeys ? useDown : altDown))
        {
            var attackerPos = TransformSystem.GetMapCoordinates(entity);

            if (mousePos.MapId != attackerPos.MapId ||
                (attackerPos.Position - mousePos.Position).Length() > weapon.Range)
            {
                if (weapon.HeavyOnLightMiss)
                    ClientHeavyAttack(entity, coordinates, weaponUid, weapon);

                return;
            }

            EntityUid? target = null;

            if (_stateManager.CurrentState is GameplayStateBase screen)
            {
                target = screen.GetClickedEntity(mousePos);
            }

            // Don't light-attack if interaction will be handling this instead
            if (Interaction.CombatModeCanHandInteract(entity, target) &&
                _intent.GetIntent(entity) != Intent.Harm)
                return;

            // We should not punch anyone with guns or ranged weapon instead from HARM
            if (altDown && _intent.GetIntent(entity) != Intent.Harm)
                return;

            // WD EDIT START
            if (weapon.AltDisarm && weaponUid == entity && _intent.GetIntent(entity) == Intent.Disarm)
            {
                RaiseNetworkEvent(new DisarmAttackEvent(GetNetEntity(target), GetNetCoordinates(coordinates)));
                return;
            }

            if (weaponUid == entity && _intent.GetIntent(entity) == Intent.Grab)
            {
                RaiseNetworkEvent(new GrabAttackEvent(GetNetEntity(target), GetNetCoordinates(coordinates)));
                return;
            }
            // WD EDIT END

            if (weapon.HeavyOnLightMiss && !CanDoLightAttack(entity, target, weapon, out _))
            {
                ClientHeavyAttack(entity, coordinates, weaponUid, weapon);
                return;
            }

            RaiseNetworkEvent(new LightAttackEvent(GetNetEntity(target), GetNetEntity(weaponUid), GetNetCoordinates(coordinates)));
        }
    }

    protected override bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session)
    {
        var xform = Transform(target);
        var targetCoordinates = xform.Coordinates;
        var targetLocalAngle = xform.LocalRotation;

        return Interaction.InRangeUnobstructed(user, target, targetCoordinates, targetLocalAngle, range);
    }

    protected override void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform)
    {
        // Server never sends the event to us for predictiveeevent.
        _color.RaiseEffect(Color.Red, targets, Filter.Local());
    }

    protected override bool DoDisarm(EntityUid user, DisarmAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        if (!base.DoDisarm(user, ev, meleeUid, component, session))
            return false;

        if (!TryComp<IntentComponent>(user, out var intent) || !intent.CanDisarm) // WD EDIT
            return false;

        var target = GetEntity(ev.Target);

        // They need to either have hands...
        if (!HasComp<HandsComponent>(target!.Value))
        {
            // or just be able to be shoved over.
            if (TryComp<StatusEffectsComponent>(target, out var status) && status.AllowedEffects.Contains("KnockedDown"))
                return true;

            if (Timing.IsFirstTimePredicted && HasComp<MobStateComponent>(target.Value))
                PopupSystem.PopupEntity(Loc.GetString("disarm-action-disarmable", ("targetName", target.Value)), target.Value);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Raises a heavy attack event with the relevant attacked entities.
    /// This is to avoid lag effecting the client's perspective too much.
    /// </summary>
    private void ClientHeavyAttack(EntityUid user, EntityCoordinates coordinates, EntityUid meleeUid, MeleeWeaponComponent component)
    {
        // Only run on first prediction to avoid the potential raycast entities changing.
        if (!_xformQuery.TryGetComponent(user, out var userXform) ||
            !Timing.IsFirstTimePredicted)
        {
            return;
        }

        var targetMap = coordinates.ToMap(EntityManager, TransformSystem);

        if (targetMap.MapId != userXform.MapID)
            return;

        var userPos = TransformSystem.GetWorldPosition(userXform);
        var direction = targetMap.Position - userPos;
        var distance = MathF.Min(component.Range * component.HeavyRangeModifier, direction.Length());

        // This should really be improved. GetEntitiesInArc uses pos instead of bounding boxes.
        // Server will validate it with InRangeUnobstructed.
        var entities = GetNetEntityList(ArcRayCast(userPos, direction.ToWorldAngle(), component.Angle, distance, userXform.MapID, user).ToList());
        RaiseNetworkEvent(new HeavyAttackEvent(GetNetEntity(meleeUid), entities.GetRange(0, Math.Min(component.MaxTargets, entities.Count)), GetNetCoordinates(coordinates)));
    }

    private void OnMeleeLunge(MeleeLungeEvent ev)
    {
        var ent = GetEntity(ev.Entity);
        var entWeapon = GetEntity(ev.Weapon);

        // Entity might not have been sent by PVS.
        if (Exists(ent) && Exists(entWeapon))
            DoLunge(ent, entWeapon, ev.Angle, ev.LocalPos, ev.Animation);
    }
}
