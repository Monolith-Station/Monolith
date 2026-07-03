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
        blip.Config.Color = Color.Cyan;
        blip.Config.Bounds = new(-0.25f, -0.25f, 0.25f, 0.25f);
        blip.VisibleFromOtherGrids = true;
    }

    protected override void OnUnstrapped(EntityUid uid, VehicleComponent component, ref UnstrappedEvent args)
    {
        RemComp<RadarBlipComponent>(uid);

        base.OnUnstrapped(uid, component, ref args);
    }
    //Lua end
}
