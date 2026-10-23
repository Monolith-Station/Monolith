using Content.Server.Shuttles.Events;
using Content.Shared.Shuttles.Systems;

namespace Content.Server._Mono.Detection;

public sealed partial class ApplyIFFFlagsToDockedShipsSystem : EntitySystem
{
    [Dependency] private SharedShuttleSystem _shuttle = default!;
    public override void Initialize()
    {
        base.Initialize();

        // We will run the event if dock A has the component.
        SubscribeLocalEvent<DockEvent>(OnDock);
        SubscribeLocalEvent<UndockEvent>(OnUndock);
    }

    private void OnDock(DockEvent args)
    {
        if (TryComp<ApplyIFFFlagsToDockedShipsComponent>(args.GridAUid, out var iffCompA))
            ApplyFlags(args.GridBUid, iffCompA);
        if (TryComp<ApplyIFFFlagsToDockedShipsComponent>(args.GridBUid, out var iffCompB))
            ApplyFlags(args.GridAUid, iffCompB);
    }

    private void OnUndock(UndockEvent args)
    {
        if (TryComp<ApplyIFFFlagsToDockedShipsComponent>(args.GridAUid, out var iffCompA))
            ApplyFlags(args.GridBUid, iffCompA, false);
        if (TryComp<ApplyIFFFlagsToDockedShipsComponent>(args.GridBUid, out var iffCompB))
            ApplyFlags(args.GridAUid, iffCompB, false);
    }

    public void ApplyFlags(EntityUid gridUid, ApplyIFFFlagsToDockedShipsComponent iffComp, bool applying = true)
    {
        if (applying)
            _shuttle.AddIFFFlag(gridUid, iffComp.Flags);
        else
            _shuttle.RemoveIFFFlag(gridUid, iffComp.Flags);
    }
}
