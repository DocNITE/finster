using Content.Shared.Alert;
using Content.Shared.Input;
using Content.Shared.Jumping;
using Content.Shared.SpecialIntent;
using JetBrains.Annotations;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.SpecialIntent;

public sealed class SpecialIntentSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly JumpingSystem _jumping = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private readonly ProtoId<AlertPrototype> _kickIntentAlert = "KickIntent";
    private readonly ProtoId<AlertPrototype> _climbIntentAlert = "ClimbIntent";
    private readonly ProtoId<AlertPrototype> _jumpIntentAlert = "JumpIntent";

    private EntityQuery<SpecialIntentComponent> _mIntentQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpecialIntentComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SpecialIntentComponent, ComponentShutdown>(OnShutdown);

        _mIntentQuery = GetEntityQuery<SpecialIntentComponent>();

        CommandBinds.Builder
                .Bind(ContentKeyFunctions.MouseMiddle, new PointerInputCmdHandler(HandleMiddleButton))
                .Register<SpecialIntentSystem>();
    }

    public bool HandleMiddleButton(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (session == null || session.AttachedEntity == null ||
            !_mIntentQuery.TryComp(session.AttachedEntity, out var mIntentComp))
            return false;

        switch (mIntentComp.IntentType)
        {
            case SpecialIntentType.Kick:
                return false;
            case SpecialIntentType.Climb:
                return false;
            case SpecialIntentType.Jump:
                return _jumping.TryJump(session.AttachedEntity.Value, _xform.ToMapCoordinates(coords).Position);
        }

        return true;
    }

    private void OnStartup(Entity<SpecialIntentComponent> ent, ref ComponentStartup args)
    {
        RefreshAlert(ent, ent.Comp);
    }

    private void OnShutdown(Entity<SpecialIntentComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent, _kickIntentAlert);
        _alerts.ClearAlert(ent, _climbIntentAlert);
        _alerts.ClearAlert(ent, _jumpIntentAlert);
    }

    public void RefreshAlert(EntityUid uid, SpecialIntentComponent comp)
    {
        _alerts.ShowAlert(uid, _kickIntentAlert, comp.IntentType == SpecialIntentType.Kick ? (short) 1 : (short) 0);
        _alerts.ShowAlert(uid, _climbIntentAlert, comp.IntentType == SpecialIntentType.Climb ? (short) 1 : (short) 0);
        _alerts.ShowAlert(uid, _jumpIntentAlert, comp.IntentType == SpecialIntentType.Jump ? (short) 1 : (short) 0);
    }

    public void SetIntent(EntityUid uid, SpecialIntentType? clickType, bool disableIfSame = false, SpecialIntentComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        if (clickType is null ||
            clickType == comp.IntentType && disableIfSame)
            comp.IntentType = SpecialIntentType.Generic;
        else
            comp.IntentType = clickType.Value;

        Dirty(uid, comp);
        RefreshAlert(uid, comp);
    }
}

[UsedImplicitly, DataDefinition]
public sealed partial class SetAlternativeIntentAlert : IAlertClick
{
    [DataField("intent", required: true)]
    public SpecialIntentType Intent { get; set; }

    public void AlertClicked(EntityUid uid)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        entityManager.System<SpecialIntentSystem>().SetIntent(uid, Intent, disableIfSame: true);
    }
}
