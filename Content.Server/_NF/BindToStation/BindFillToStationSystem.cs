using Content.Server.Station.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared._NF.BindToStation;
using Content.Shared.Containers;
using Robust.Server.Containers;
using Robust.Shared.Containers;

namespace Content.Server._NF.BindToStation;

/// <summary>
/// A class that binds marked containers' contents to the station they start on.
/// </summary>
public sealed class BindFillToStationSystem : EntitySystem
{
    [Dependency] private readonly BindToStationSystem _bindToStation = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BindFillToStationComponent, MapInitEvent>(OnFillMapInit, after: [typeof(StorageSystem), typeof(ContainerFillSystem)]);
    }

    public void OnFillMapInit(Entity<BindFillToStationComponent> ent, ref MapInitEvent args)
    {
        var station = _station.GetOwningStation(ent);
        if (station == null)
            return;

        if (!TryComp<ContainerManagerComponent>(ent, out var containerManager))
            return;

        foreach (var container in _container.GetAllContainers(ent, containerManager))
        {
            BindContainerContents(container, station.Value);
        }
    }

    public void BindContainerContents(BaseContainer container, EntityUid station)
    {
        foreach (var uid in container.ContainedEntities)
        {
            if (!HasComp<BindToStationComponent>(uid))
                continue;

            _bindToStation.BindToStation(uid, station);

            if (TryComp<ContainerManagerComponent>(uid, out var containerManager))
            {
                foreach (var innerContainer in _container.GetAllContainers(uid))
                    BindContainerContents(innerContainer, station);
            }
        }
    }
}
