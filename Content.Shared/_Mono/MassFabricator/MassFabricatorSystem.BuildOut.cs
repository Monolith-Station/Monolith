using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Mono.MassFabricator;

public abstract partial class MassFabricatorSystem
{
    protected static readonly Vector2i[] BuildNeighbors = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];
    [Dependency] private IMapManager _buildMaps = default!;
    [Dependency] private CESharedZLevelsSystem _levels = default!;

    private EntityUid? GetPlacementGrid(EntityUid user, MassFabricatorBlueprintKind kind, EntityCoordinates requested)
    {
        if (_transforms.GetGrid(requested) is { } explicitGrid &&
            (!HasComp<MapComponent>(explicitGrid) || Transform(user).MapUid != explicitGrid))
            return explicitGrid;

        var world = _transforms.ToMapCoordinates(requested);
        if (world.MapId == MapId.Nullspace)
            return null;

        if (_buildMaps.TryFindGridAt(world, out var atCursor, out _) && !HasComp<MapComponent>(atCursor))
            return atCursor;

        var map = _transforms.GetMap(requested);
        if (kind != MassFabricatorBlueprintKind.Tile || HasComp<MapGridComponent>(map))
            return HasComp<MapGridComponent>(map) ? map : null;

        EntityUid? nearest = null;
        var distance = float.PositiveInfinity;
        if (TryComp<MassFabricatorViewComponent>(user, out var view) &&
            TryComp<MassFabricatorComponent>(view.Fabricator, out var fab))
        {
            foreach (var plan in fab.Plans)
                if (TryComp<MassFabricatorHologramComponent>(plan, out var hologram) && hologram.BuildGrid is { } frame &&
                    TryComp<MapGridComponent>(frame, out var grid))
                    Consider((frame, grid));
        }

        _buildMaps.FindGridsIntersecting(world.MapId, Box2.CenteredAround(world.Position, new Vector2(4f)),
            (uid, grid) =>
            {
                Consider((uid, grid));
                return true;
            }, approx: true, includeMap: false);
        return nearest;

        void Consider(Entity<MapGridComponent> grid)
        {
            if (Transform(grid).MapID != world.MapId)
                return;

            var cell = _maps.TileIndicesFor(grid, grid.Comp, world);
            if (!TryGetPlanSupport(user, grid, cell, out _))
                return;

            var center = _transforms.ToMapCoordinates(_maps.GridTileToLocal(grid, grid.Comp, cell));
            var candidate = Vector2.DistanceSquared(center.Position, world.Position);
            if (candidate < distance || candidate == distance && nearest is { } other && grid.Owner.Id < other.Id)
            {
                distance = candidate;
                nearest = grid;
            }
        }
    }

    private bool TryGetPlanSupport(EntityUid user, Entity<MapGridComponent> grid, Vector2i cell, out Vector2i? support,
        IReadOnlySet<EntityCoordinates>? staged = null)
    {
        support = null;
        if (HasComp<MapComponent>(grid) || Occupied(grid, cell))
            return true;

        foreach (var offset in BuildNeighbors)
        {
            if (Occupied(grid, cell + offset))
            {
                support = cell + offset;
                return true;
            }
        }

        if (!TryComp<MassFabricatorViewComponent>(user, out var view) ||
            !TryComp<MassFabricatorComponent>(view.Fabricator, out var fab))
            return false;

        var pending = new HashSet<Vector2i>();
        foreach (var uid in fab.Plans)
        {
            if (!TryComp<MassFabricatorHologramComponent>(uid, out var plan) || plan.BuildGrid != grid.Owner ||
                EntityManager.IsQueuedForDeletion(uid) || plan.SupportCell is not { } root || !Occupied(grid, root))
                continue;

            pending.Add(plan.Cell);
        }

        if (staged != null)
            foreach (var coordinates in staged)
                if (coordinates.EntityId == grid.Owner)
                    pending.Add(_maps.CoordinatesToTile(grid, grid.Comp, coordinates));

        var connected = new Queue<Vector2i>();
        connected.Enqueue(cell);
        pending.Remove(cell);
        while (connected.TryDequeue(out var current))
        {
            foreach (var offset in BuildNeighbors)
            {
                var next = current + offset;
                if (Occupied(grid, next))
                {
                    support = next;
                    return true;
                }

                if (pending.Remove(next))
                    connected.Enqueue(next);
            }
        }

        return false;
    }

    private void AdvanceBuildSupport(Entity<MassFabricatorHologramComponent> completed, EntityUid frame)
    {
        if (completed.Comp.SupportCell is not { } root ||
            !TryComp<MassFabricatorComponent>(completed.Comp.Fabricator, out var fab))
            return;

        var pending = new Dictionary<Vector2i, Entity<MassFabricatorHologramComponent>>();
        foreach (var uid in fab.Plans)
            if (uid != completed.Owner && TryComp<MassFabricatorHologramComponent>(uid, out var plan) &&
                plan.BuildGrid == frame && plan.SupportCell == root && !EntityManager.IsQueuedForDeletion(uid))
                pending.TryAdd(plan.Cell, (uid, plan));

        var connected = new Queue<Vector2i>();
        connected.Enqueue(completed.Comp.Cell);
        while (connected.TryDequeue(out var current))
        {
            foreach (var offset in BuildNeighbors)
            {
                if (!pending.Remove(current + offset, out var plan))
                    continue;

                plan.Comp.SupportCell = completed.Comp.Cell;
                Dirty(plan);
                connected.Enqueue(plan.Comp.Cell);
            }
        }
    }

    private bool Occupied(Entity<MapGridComponent> grid, Vector2i cell) =>
        _maps.TryGetTile(grid.Comp, cell, out var tile) && !tile.IsEmpty;

    protected bool CanTargetBuildLayer(EntityUid user, EntityUid? target)
    {
        var map = Transform(user).MapUid;
        if (target == map)
            return true;

        if (map is not { } source || target is not { } upper || !HasComp<MapGridComponent>(upper) ||
            !TryComp<CEZLevelViewerComponent>(user, out var viewer) || !viewer.LookUp)
            return false;

        return TryComp<CEZTransitMapComponent>(source, out var transit)
            ? transit.UpperMap == upper
            : _levels.TryMapUp(source, out var above) && above.Owner == upper;
    }

    protected virtual bool? HasStructuralSupport(EntityUid grid, Vector2i cell) => true;

    protected void UpdateStructuralSupport(Entity<MassFabricatorHologramComponent> plan, bool printable, bool projected)
    {
        if (plan.Comp.Unsupported == !projected && plan.Comp.PrintBlocked == !printable)
            return;

        plan.Comp.Unsupported = !projected;
        plan.Comp.PrintBlocked = !printable;
        if (!printable && plan.Comp.PrintState == MassFabricatorPrintState.Printing)
            ResetPrint(plan);
        else
            Dirty(plan);
    }

    private bool CanPrintCell(Entity<MassFabricatorHologramComponent> plan)
    {
        if (plan.Comp.Kind != MassFabricatorBlueprintKind.Tile)
            return CanPlacePlanOnTile(Transform(plan).Coordinates);

        if (plan.Comp.BuildGrid is not { } frame || !TryComp<MapGridComponent>(frame, out var grid) ||
            Transform(plan).ParentUid != frame || HasStructuralSupport(frame, plan.Comp.Cell) != true ||
            _net.IsClient && plan.Comp.PrintBlocked)
            return false;

        return HasPrintFooting((frame, grid), plan.Comp.Cell) && !OverlapsForeignGrid((frame, grid), plan.Comp.Cell);
    }

    public bool HasPrintFooting(Entity<MapGridComponent> grid, Vector2i cell)
    {
        if (HasComp<MapComponent>(grid) || Occupied(grid, cell))
            return true;

        foreach (var offset in BuildNeighbors)
            if (Occupied(grid, cell + offset))
                return true;

        return false;
    }

    public bool OverlapsForeignGrid(Entity<MapGridComponent> frame, Vector2i cell)
    {
        var size = frame.Comp.TileSize;
        var origin = (Vector2) cell * size;
        var worldMatrix = _transforms.GetWorldMatrix(frame);
        var worldBounds = worldMatrix.TransformBox(new Box2(origin, origin + new Vector2(size)));
        var overlaps = false;
        _buildMaps.FindGridsIntersecting(Transform(frame).MapID, worldBounds, (uid, grid) =>
        {
            if (uid == frame.Owner)
                return true;

            var matrix = worldMatrix * _transforms.GetInvWorldMatrix(uid);
            var first = Vector2.Transform(origin, matrix);
            var x = Vector2.TransformNormal(new Vector2(size, 0), matrix);
            var y = Vector2.TransformNormal(new Vector2(0, size), matrix);
            var center = first + (x + y) * 0.5f;
            var extent = (Vector2.Abs(x) + Vector2.Abs(y)) * 0.5f;
            var tiles = _maps.GetLocalTilesEnumerator(uid, grid, new Box2(center - extent, center + extent));
            var half = grid.TileSize / 2f;
            while (tiles.MoveNext(out var tile))
            {
                var tileCenter = (Vector2) tile.GridIndices * grid.TileSize + new Vector2(half);
                var delta = tileCenter - center;
                if (MathF.Abs(delta.X) >= extent.X + half - 0.001f ||
                    MathF.Abs(delta.Y) >= extent.Y + half - 0.001f ||
                    !OverlapsAxis(x, delta, half) || !OverlapsAxis(y, delta, half))
                    continue;

                overlaps = true;
                return false;
            }

            return true;
        }, approx: true, includeMap: true);
        return overlaps;

        bool OverlapsAxis(Vector2 axis, Vector2 delta, float half)
        {
            axis /= size;
            return MathF.Abs(Vector2.Dot(delta, axis)) < size / 2f + half * (MathF.Abs(axis.X) + MathF.Abs(axis.Y)) - 0.001f;
        }
    }
}
