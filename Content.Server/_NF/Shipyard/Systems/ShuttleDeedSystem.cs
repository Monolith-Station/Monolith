using Content.Server._NF.Shipyard.Systems;
using Content.Server._NF.Shipyard.Components;
using Content.Server._NF.Shipyard;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._Mono.Ships.Components;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Server._NF.Shipyard;

public sealed partial class ShuttleDeedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShuttleDeedComponent, ExaminedEvent>(OnExamined);
    }

    public bool HasOwner(Entity<VesselComponent?> vessel)
    {
        return !TryComp<ShuttleDeedComponent>(vessel, out var deed) || deed.DeedHolder == null;
    }

    private void OnExamined(Entity<ShuttleDeedComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;

        if (!args.IsInDetailsRange)
            return;

        if (!string.IsNullOrEmpty(comp.ShuttleName))
        {
            var fullName = ShipyardSystem.GetFullName(comp);
            args.PushMarkup(Loc.GetString("shuttle-deed-examine-text", ("shipname", fullName)));
        }
    }
}
