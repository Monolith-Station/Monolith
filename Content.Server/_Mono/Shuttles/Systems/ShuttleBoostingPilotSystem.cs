using Content.Server._Mono.Shuttles.Components;
using Content.Server.Physics.Controllers;

namespace Content.Server._Mono.Shuttles.Systems;

public sealed partial class ShuttleBoostingPilotSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleBoostingPilotComponent, GetShuttleInputsEvent>(OnGetInputs);
    }

    private void OnGetInputs(Entity<ShuttleBoostingPilotComponent> ent, ref GetShuttleInputsEvent args)
    {
        args.AngularMul *= ent.Comp.AngularMultiplier;
        args.AccelMul *= ent.Comp.AccelerationMultiplier;
    }
}
