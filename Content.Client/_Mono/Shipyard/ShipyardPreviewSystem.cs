using System.Linq;
using Content.Client.Mind;
using Content.Shared._Mono.Shipyard;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.Anomaly;
using Robust.Client.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Client._Mono.Shipyard;

/// <summary>
/// This handles spawning client-side grid and getting data from it.
/// </summary>
public sealed class ShipyardPreviewSystem : SharedShipyardPreviewSystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private MindSystem _mind = default!;

    public Entity<MapGridComponent>? CurrentGrid;
    public override void Initialize()
    {
        base.Initialize();

    }

    public bool TryPreviewGrid(VesselPrototype vessel)
    {
        CachePreviewMap();

        var opts = new DeserializationOptions();
        if (!_loader.TryLoadGrid(_previewMap,
                vessel.ShuttlePath,
                out var grid,
                opts))
            return false;

        _meta.SetEntityName(grid.Value, vessel.Name);
        CurrentGrid = grid.Value;
        return true;
    }

    public FormattedMessage GetGridData()
    {
        var msg = new FormattedMessage();
        if (CurrentGrid == null)
            return msg;

        msg.AddMarkupOrThrow(
            Loc.GetString("shipyard-preview-tile-count", ("count", _map.GetAllTiles(CurrentGrid.Value.Owner, CurrentGrid.Value.Comp).Count().ToString()))
            );

        return msg;
    }

    public void Dispose()
    {
        Del(CurrentGrid);
        CurrentGrid = null;
    }
}

