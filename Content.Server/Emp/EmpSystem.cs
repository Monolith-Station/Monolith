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

namespace Content.Server.Emp;

public sealed partial class EmpSystem : SharedEmpSystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!; // Frontier: EMP Blast PVS
    [Dependency] private IConfigurationManager _cfg = default!; // Frontier: EMP Blast PVS
    [Dependency] private ExamineSystem _examine = default!; // Frontier: examine verb

    public const string EmpPulseEffectPrototype = "EffectEmpBlast"; // Frontier: EffectEmpPulse

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmpOnTriggerComponent, TriggerEvent>(HandleEmpTrigger);
        SubscribeLocalEvent<EmpOnTriggerComponent, GetVerbsEvent<ExamineVerb>>(OnEmpTriggerExamine); // Frontier
        SubscribeLocalEvent<EmpDescriptionComponent, GetVerbsEvent<ExamineVerb>>(OnEmpDescriptorExamine); // Frontier

        SubscribeLocalEvent<EmpDisabledComponent, RadioSendAttemptEvent>(OnRadioSendAttempt);
        SubscribeLocalEvent<EmpDisabledComponent, RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
    }

    // Frontier: examine EMP trigger objects
    private void OnEmpTriggerExamine(EntityUid uid, EmpOnTriggerComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var msg = GetEmpDescription(component.Range, component.EnergyConsumption, component.DisableDuration);

        _examine.AddDetailedExamineVerb(args, component, msg,
            Loc.GetString("emp-examinable-verb-text"), "/Textures/Interface/VerbIcons/smite.svg.192dpi.png",
            Loc.GetString("emp-examinable-verb-message"));
    }
    private void OnEmpDescriptorExamine(EntityUid uid, EmpDescriptionComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var msg = GetEmpDescription(component.Range, component.EnergyConsumption, component.DisableDuration);

        _examine.AddDetailedExamineVerb(args, component, msg,
            Loc.GetString("emp-examinable-verb-text"), "/Textures/Interface/VerbIcons/smite.svg.192dpi.png",
            Loc.GetString("emp-examinable-verb-message"));
    }

    private FormattedMessage GetEmpDescription(float range, float energy, float time)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("emp-examine"));
        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("emp-range-value",
            ("value", range)));
        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("emp-energy-value",
            ("value", energy)));
        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("emp-time-value",
            ("value", time)));
        return msg;
    }
    // End Frontier

    private void HandleEmpTrigger(EntityUid uid, EmpOnTriggerComponent comp, TriggerEvent args)
    {
        EmpPulse(_transform.GetMapCoordinates(uid), comp.Range, comp.EnergyConsumption, TimeSpan.FromSeconds(comp.DisableDuration));
        args.Handled = true;
    }

    private void OnRadioSendAttempt(EntityUid uid, EmpDisabledComponent component, ref RadioSendAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnRadioReceiveAttempt(EntityUid uid, EmpDisabledComponent component, ref RadioReceiveAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
