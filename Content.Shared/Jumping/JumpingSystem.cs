using Content.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Content.Shared.Throwing;
using Content.Shared.Movement.Systems;
using System.Numerics;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Content.Shared.Movement.Components;
using Content.Shared.Standing;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;
using Content.Shared.Stunnable;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Jumping;

public sealed class JumpingSystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwSys = default!;
    [Dependency] private readonly SharedPhysicsSystem _phys = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly DamageableSystem _damSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _rand = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<JumpingComponent> _jumpingQuery;
    private EntityQuery<StandingStateComponent> _standingQuery;

    public override void Initialize()
    {
        base.Initialize();

        /*
        CommandBinds.Builder
                .Bind(ContentKeyFunctions.Jump, new PointerInputCmdHandler(HandleJumpButton, outsidePrediction: false))
                .Register<JumpingSystem>();
        */
        SubscribeLocalEvent<JumpingComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<JumpingComponent, StartCollideEvent>(OnStartCollide);

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _jumpingQuery = GetEntityQuery<JumpingComponent>();
        _standingQuery = GetEntityQuery<StandingStateComponent>();
    }

    /*
    public bool HandleJumpButton(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (session == null || session.AttachedEntity == null ||
            !_jumpingQuery.TryComp(session.AttachedEntity, out var jumpComp))
            return false;

        return TryJump(session.AttachedEntity.Value, coords.Position, jumpComp);
    }
    */

    private void OnStartCollide(EntityUid uid, JumpingComponent component, ref StartCollideEvent args)
    {
        if (component.JumpingState < JumpingState.Jumping)
            return;

        if (!_physicsQuery.TryComp(uid, out var phys) || !args.OtherFixture.Hard)
            return;
        if (_standingQuery.TryComp(args.OtherEntity, out var standing) &&
            standing.CurrentState < StandingState.Standing)
            return;

        if (phys.BodyStatus == BodyStatus.InAir)
        {
            DamageSpecifier damage = component.BaseCollideDamage;
            _stun.TryParalyze(uid, TimeSpan.FromSeconds(3), true);
            _damSystem.TryChangeDamage(uid, damage * component.DamageModifier, ignoreResistances: true, targetPart: TargetBodyPart.Head);
            _damSystem.TryChangeDamage(uid, damage * component.DamageModifier, ignoreResistances: true, targetPart: TargetBodyPart.Torso);
            _phys.SetLinearVelocity(uid, new Vector2(0, 0));

            // TODO: Stun time should be calculated by mass contests
            _stun.TryParalyze(args.OtherEntity, TimeSpan.FromSeconds(3), true);
            _damSystem.TryChangeDamage(args.OtherEntity, damage * component.DamageModifier, ignoreResistances: true, targetPart: TargetBodyPart.Head);
            _damSystem.TryChangeDamage(args.OtherEntity, damage * component.DamageModifier, ignoreResistances: true, targetPart: TargetBodyPart.Torso);
        }
    }

    public bool TryJump(EntityUid uid, Vector2 position, JumpingComponent? jumpComp = null)
    {
        if (Deleted(uid) ||
            !Resolve(uid, ref jumpComp))
            return false;

        /*
        if (_parent.IsValid())
                {
                    // parent coords to world coords
                    return Vector2.Transform(_localPosition, _entMan.GetComponent<TransformComponent>(ParentUid).WorldMatrix);
                }
                else
                {
                    return Vector2.Zero;
                }
        */

        var userTransf = Transform(uid);
        var direction = userTransf.WorldPosition - position;
        if (direction.Length() > jumpComp.JumpRange)
            return false;

        if ((jumpComp.LastJump != null && _gameTiming.CurTime - jumpComp.LastJump < jumpComp.JumpCooldown)
            || (TryComp<InputMoverComponent>(uid, out var inputMoverComp) && !inputMoverComp.CanMove)
            || (TryComp<StandingStateComponent>(uid, out var standingComp) && standingComp.CurrentState != StandingState.Standing))
            return false;

        var beforeJumpingEv = new BeforeJumpingEvent(uid, position, Handled: false);
        RaiseLocalEvent(uid, ref beforeJumpingEv, broadcast: true);
        if (beforeJumpingEv.Handled)
            return false;

        // TODO: Implement skill checking and chance on failure
        //if (_rand.Prob(0.05f))
        //    jumpComp.IsFailed = true;
        _phys.SetLinearVelocity(uid, Vector2.Zero);
        _throwSys.TryThrow(uid, -direction, baseThrowSpeed: jumpComp.JumpSpeed);

        jumpComp.LastJump = _gameTiming.CurTime;
        if (jumpComp.JumpingState <= JumpingState.Jumping)
            jumpComp.JumpingState = JumpingState.Jumping;

        var afterJumpingEv = new AfterJumpingEvent(uid, position);
        RaiseLocalEvent(uid, ref afterJumpingEv, broadcast: true);

        return true;
    }

    private void OnLand(EntityUid uid, JumpingComponent jumpComp, ref LandEvent args)
    {
        jumpComp.JumpingState = JumpingState.Landed;

        var landingJumpingEv = new LandingAfterJumpEvent(uid);
        RaiseLocalEvent(uid, ref landingJumpingEv, broadcast: true);

        jumpComp.JumpingState = JumpingState.Standing;
    }
}

[ByRefEvent]
public record struct BeforeJumpingEvent(EntityUid User, Vector2 TargetPosition, bool Handled);

[ByRefEvent]
public record struct AfterJumpingEvent(EntityUid User, Vector2 TargetPosition);

[ByRefEvent]
public record struct LandingAfterJumpEvent(EntityUid User);
