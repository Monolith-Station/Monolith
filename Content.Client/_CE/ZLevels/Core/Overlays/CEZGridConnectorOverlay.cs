/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Client.Graphics;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._CE.ZLevels.Core.Overlays;

/// <summary>
/// Shitcode mapping aid: draws a circle at every grid connector's world position — the
/// exact point <c>CEZGridConnectorSystem</c> tests for a tile on the neighbouring layer — so
/// you can line the grids up over their connectors. All z-maps share one world coordinate
/// space, so a connector on the layer below draws at the same spot in the layer you're
/// editing. Cyan = the connector currently binds a grid there; lime = it's dangling over
/// empty space. Toggle with <c>showgridconnectors</c>.
/// </summary>
public sealed class CEZGridConnectorOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = null!;
    [Dependency] private readonly IMapManager _mapManager = null!;

    private readonly SharedTransformSystem _transform;
    private readonly SharedMapSystem _mapSystem;
    private readonly CEClientZLevelsSystem _zLevels;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public CEZGridConnectorOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entityManager.System<SharedTransformSystem>();
        _mapSystem = _entityManager.System<SharedMapSystem>();
        _zLevels = _entityManager.System<CEClientZLevelsSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        var query = _entityManager.EntityQueryEnumerator<CEZGridConnectorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var connector, out var xform))
        {
            var worldPos = _transform.GetWorldPosition(xform);
            var color = IsLinking(connector, xform, worldPos) ? Color.Cyan : Color.Lime;

            // Outline ring = the tile the connector wants to bind to on the neighbouring layer.
            handle.DrawCircle(worldPos, 0.45f, color.WithAlpha(0.4f), filled: false);
            // Filled dot = the exact checked point.
            handle.DrawCircle(worldPos, 0.1f, color);
        }
    }

    /// <summary>
    /// Client-side mirror of <c>CEZGridConnectorSystem.TryGetConnectorLink</c>: whether the
    /// connector currently has a grid with a real tile directly above it (and below, for
    /// AnchorBelow connectors).
    /// </summary>
    private bool IsLinking(CEZGridConnectorComponent connector, TransformComponent xform, Vector2 worldPos)
    {
        if (!xform.Anchored || xform.GridUid is not { } ownGrid || xform.MapUid == null || xform.ParentUid == xform.MapUid)
            return false;

        return HasBoundGrid(xform.MapUid.Value, worldPos, up: true, ownGrid)
               || (connector.AnchorBelow && HasBoundGrid(xform.MapUid.Value, worldPos, up: false, ownGrid));
    }

    private bool HasBoundGrid(EntityUid mapUid, Vector2 worldPos, bool up, EntityUid ownGrid)
    {
        EntityUid neighbourMap;
        if (_entityManager.TryGetComponent<CEZMapComponent>(mapUid, out var zMap))
        {
            Entity<CEZMapComponent> neighbour;
            if (up ? !_zLevels.TryMapUp((mapUid, zMap), out neighbour) : !_zLevels.TryMapDown((mapUid, zMap), out neighbour))
                return false;

            neighbourMap = neighbour.Owner;
        }
        else if (_entityManager.TryGetComponent<CEZTransitMapComponent>(mapUid, out var transit)
                 && (up ? transit.TransitAbove : transit.TransitBelow) is { } transitNeighbour)
        {
            neighbourMap = transitNeighbour;
        }
        else
        {
            return false;
        }

        if (!_mapManager.TryFindGridAt(neighbourMap, worldPos, out var gridUid, out var gridComp) || gridUid == ownGrid)
            return false;

        return _mapSystem.TryGetTileRef(gridUid, gridComp, worldPos, out var tileRef) && !tileRef.Tile.IsEmpty;
    }
}

public sealed class CEShowGridConnectorsCommand : LocalizedCommands
{
    [Dependency] private readonly IOverlayManager _overlayManager = null!;

    public override string Command => "showgridconnectors";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_overlayManager.HasOverlay<CEZGridConnectorOverlay>())
        {
            _overlayManager.RemoveOverlay<CEZGridConnectorOverlay>();
            return;
        }

        _overlayManager.AddOverlay(new CEZGridConnectorOverlay());
    }
}
