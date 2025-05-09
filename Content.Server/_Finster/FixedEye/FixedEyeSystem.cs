using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Input;
using Content.Shared.Movement.Components;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Finster.FixedEye;

public sealed class FixedEyeSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;

    private ProtoId<AlertPrototype> FixedAlert = "Fixed";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FixedEyeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FixedEyeComponent, ComponentShutdown>(OnShutdown);

        // FIXME: Move into mover controller and make events?
        SubscribeLocalEvent<NoRotateOnMoveComponent, ComponentAdd>(OnNoRotateAdded);
        SubscribeLocalEvent<NoRotateOnMoveComponent, ComponentRemove>(OnNoRotateRemoved);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.FixedDirection, new PointerInputCmdHandler(HandleToggleFixedEye))
            .Register<FixedEyeSystem>();
    }

    private void OnStartup(Entity<FixedEyeComponent> ent, ref ComponentStartup args)
    {
        RefreshAlert(ent);
    }

    private void OnShutdown(Entity<FixedEyeComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent, FixedAlert);
    }

    private void OnNoRotateAdded(Entity<NoRotateOnMoveComponent> ent, ref ComponentAdd args)
    {
        RefreshAlert(ent);
    }

    private void OnNoRotateRemoved(Entity<NoRotateOnMoveComponent> ent, ref ComponentRemove args)
    {
        RefreshAlert(ent);
    }

    private bool HandleToggleFixedEye(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
    {
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player))
            return false;

        Toggle(player);

        return true;
    }

    public void RefreshAlert(EntityUid uid, FixedEyeComponent? fixedEyeComp = null)
    {
        // Just checking
        if (!Resolve(uid, ref fixedEyeComp, false))
            return;

        var hasNoRotate = HasComp<NoRotateOnMoveComponent>(uid);
        _alerts.ShowAlert(uid, FixedAlert, hasNoRotate ? (short) 1 : (short) 0);
    }

    public void Toggle(EntityUid uid, FixedEyeComponent? fixedEyeComp = null)
    {
        if (!Resolve(uid, ref fixedEyeComp, false))
            return;

        if (!HasComp<NoRotateOnMoveComponent>(uid))
            EnsureComp<NoRotateOnMoveComponent>(uid);
        else
            RemComp<NoRotateOnMoveComponent>(uid);
    }
}

[DataDefinition]
public sealed partial class ToggleFixedEyeAlert : IAlertClick
{
    public void AlertClicked(EntityUid uid)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        entityManager.System<FixedEyeSystem>().Toggle(uid);
    }
}
