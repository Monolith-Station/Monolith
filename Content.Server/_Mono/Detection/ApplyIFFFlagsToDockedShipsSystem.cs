using Content.Server.Shuttles.Events;
using Content.Shared.Shuttles.Components;
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
        if (!TryComp<ApplyIFFFlagsToDockedShipsComponent>(args.GridAUid, out var iffComp))
            return;

        ApplyFlags(args.GridBUid, iffComp, true);
    }

    private void OnUndock(UndockEvent args)
    {
        if (!TryComp<ApplyIFFFlagsToDockedShipsComponent>(args.GridAUid, out var iffComp))
            return;

        ApplyFlags(args.GridBUid, iffComp, false);
    }

    public void ApplyFlags(EntityUid gridUid, ApplyIFFFlagsToDockedShipsComponent iffComp, bool applying = true)
    {
        if (applying)
            _shuttle.AddIFFFlag(gridUid, iffComp.Flags);
        else
            _shuttle.RemoveIFFFlag(gridUid, iffComp.Flags);
    }
}