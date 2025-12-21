using Content.Server._Mono.Radar; //Lua mod
using Content.Shared.Buckle.Components; //Lua mod
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components; //Lua mod

namespace Content.Server.Vehicle;

public sealed class VehicleSystem : SharedVehicleSystem
{
    //Lua start
    protected override void OnStrapped(EntityUid uid, VehicleComponent component, ref StrappedEvent args)
    {
        base.OnStrapped(uid, component, ref args);

        var blip = EnsureComp<RadarBlipComponent>(uid);
        blip.RadarColor = Color.Cyan;
        blip.Scale = 0.5f;
        blip.VisibleFromOtherGrids = true;
    }

    protected override void OnUnstrapped(EntityUid uid, VehicleComponent component, ref UnstrappedEvent args)
    {
        RemComp<RadarBlipComponent>(uid);

        base.OnUnstrapped(uid, component, ref args);
    }
    //Lua end
}
