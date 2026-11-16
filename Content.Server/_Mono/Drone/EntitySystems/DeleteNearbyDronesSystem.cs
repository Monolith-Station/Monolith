using Content.Server._Mono.Drone.Components;
using Content.Server.StationEvents.Events;
using Robust.Server.GameObjects;

namespace Content.Server._Mono.Drone.EntitySystems;

public sealed partial class DeleteNearbyDronesSystem : EntitySystem
{
    [Dependency] private LinkedLifecycleGridSystem _lifecycleGridSystem = default!;
    [Dependency] private TransformSystem _xform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var gridQuery = EntityQueryEnumerator<DeleteNearbyDronesComponent>();
        while (gridQuery.MoveNext(out var gridUid, out var delComp))
        {
            var droneQuery = EntityQueryEnumerator<HostileDroneGridComponent>();
            while (droneQuery.MoveNext(out var droneUid, out _))
            {
                var distance = _xform.GetWorldPosition(gridUid) - _xform.GetWorldPosition(droneUid);
                if (distance.Length() > delComp.Distance)
                    continue;
                _lifecycleGridSystem.UnparentPlayersFromGrid(droneUid, true, false); // this also handles deleting the drone lmao
            }
        }
    }
}