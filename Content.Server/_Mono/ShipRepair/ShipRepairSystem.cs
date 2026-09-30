using Content.Server._NF.Shipyard;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.ShipRepair;

namespace Content.Server._Mono.ShipRepair;

public sealed partial class ShipRepairSystem : SharedShipRepairSystem
{
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleComponent, ShipBoughtEvent>(OnShipBought);
        SubscribeLocalEvent<InitRepairSnapshotComponent, MapInitEvent>(OnInitSnapshot);

        InitCommands();
        InitGhosts();
    }

    private void OnShipBought(Entity<ShuttleComponent> ent, ref ShipBoughtEvent ev)
    {
        GenerateRepairData(ent);
    }

    private void OnInitSnapshot(Entity<InitRepairSnapshotComponent> ent, ref MapInitEvent ev)
    {
        GenerateRepairData(ent);
    }
}
