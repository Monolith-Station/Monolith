using Content.Shared.VendingMachines;
using Robust.Shared.Timing;

namespace Content.Shared._Mono.VendingMachine;

public sealed partial class VendingMachineAutomaticRestockSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedVendingMachineSystem _vend = default!;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<VendingMachineComponent, VendingMachineAutomaticRestockComponent>();
        while (query.MoveNext(out var uid, out var vendComp, out var restockComp))
        {
            if (restockComp.LastRestock + restockComp.Interval > curTime)
                continue;
            if (vendComp == null || restockComp == null)
                continue;

            if (restockComp.LastRestock != TimeSpan.Zero) // don't do this the first time it checks
            {
                if (restockComp.Replace)
                    vendComp.Inventory = new Dictionary<string, VendingMachineInventoryEntry>();

                _vend.RestockInventoryFromPrototype(uid, vendComp);
            }

            restockComp.LastRestock = curTime;
            Dirty(uid, restockComp); // the restock function dirties the vendComp already
        }
    }
}