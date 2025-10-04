using Content.Server.Entry;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio;
using Content.Server.Station.Components;
using Content.Server.SurveillanceCamera;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Tiles; // Frontier
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Content.Shared._NF.Emp.Components; // Frontier
using Robust.Server.GameStates; // Frontier: EMP Blast PVS
using Robust.Shared.Configuration; // Frontier: EMP Blast PVS
using Robust.Shared; // Frontier: EMP Blast PVS
using Content.Shared.Verbs; // Frontier: examine verb
using Robust.Shared.Utility; // Frontier: examine verb
using Content.Server.Examine; // Frontier: examine verb
using Content.Server._Mono.Emp; // Mono: EMP Shielding

namespace Content.Server.Emp;

public sealed partial class EmpSystem : SharedEmpSystem
{
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!; // Frontier: EMP Blast PVS
    [Dependency] private IConfigurationManager _cfg = default!; // Frontier: EMP Blast PVS
    [Dependency] private ExamineSystem _examine = default!; // Frontier: examine verb

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmpDisabledComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<EmpOnTriggerComponent, TriggerEvent>(HandleEmpTrigger);
        SubscribeLocalEvent<EmpOnTriggerComponent, GetVerbsEvent<ExamineVerb>>(OnEmpTriggerExamine); // Frontier
        SubscribeLocalEvent<EmpDescriptionComponent, GetVerbsEvent<ExamineVerb>>(OnEmpDescriptorExamine); // Frontier

        SubscribeLocalEvent<EmpDisabledComponent, RadioSendAttemptEvent>(OnRadioSendAttempt);
        SubscribeLocalEvent<EmpDisabledComponent, RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
        SubscribeLocalEvent<EmpDisabledComponent, ApcToggleMainBreakerAttemptEvent>(OnApcToggleMainBreaker);
        SubscribeLocalEvent<EmpDisabledComponent, SurveillanceCameraSetActiveAttemptEvent>(OnCameraSetActive);
    }

    private void OnRadioSendAttempt(EntityUid uid, EmpDisabledComponent component, ref RadioSendAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnRadioReceiveAttempt(EntityUid uid, EmpDisabledComponent component, ref RadioReceiveAttemptEvent args)
    {
        args.Cancelled = true;
    }

    //private void OnApcToggleMainBreaker(EntityUid uid, EmpDisabledComponent component, ref ApcToggleMainBreakerAttemptEvent args) // Frontier: Upstream - #28984
    //{
    //    args.Cancelled = true;
    //}

    //private void OnCameraSetActive(EntityUid uid, EmpDisabledComponent component, ref SurveillanceCameraSetActiveAttemptEvent args) // Frontier: Upstream - #28984
    //{
    //    args.Cancelled = true;
    //}

}
