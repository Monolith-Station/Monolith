using Content.Server.Power.Components;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._Mono.Emp;


public sealed class EmpResistanceSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<EmpResistanceComponent, GetVerbsEvent<ExamineVerb>>(OnExamine);
    }

    private void OnExamine(Entity<EmpResistanceComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!TryComp<BatteryComponent>(ent.Owner, out var battery))
            return;

        var msg = FormatEmp(ent, battery);

        _examine.AddDetailedExamineVerb(args, ent.Comp, msg,
            Loc.GetString("battery-examinable-verb-text"),
            "/Textures/Interface/VerbIcons/smite.svg.192dpi.png",
            Loc.GetString("battery-examinable-verb-message"));
    }

    private FormattedMessage FormatEmp(EmpResistanceComponent res, BatteryComponent battery)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(res.Coefficient == 0
            ? Loc.GetString("battery-examine-emp-null")
            : Loc.GetString("battery-examine-emp", ("energy", battery.MaxCharge / res.Coefficient)));
        return msg;
    }
}
