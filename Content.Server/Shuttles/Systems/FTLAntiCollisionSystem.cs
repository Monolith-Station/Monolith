using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Maps;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server.Shuttles.Systems;

/// <summary>
/// This system prevents ships from colliding when they FTL to the same location
/// by separating them once they arrive.
///
/// When a ship completes FTL travel, this system checks if there are any other ships
/// nearby that are too close. If so, it repositions the arriving ship to a safe distance
/// in a random direction.
///
/// This solves the problem of ships "merging" together when they FTL to the same destination
/// coordinates, which can cause visual glitches, physics issues, and gameplay problems.
/// </summary>
public sealed partial class FTLAntiCollisionSystem : EntitySystem
{
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private DockingSystem _dockingSystem = default!;
    [Dependency] private EntityLookupSystem _lookupSystem = default!;

    // How far to check for other ships
    private const float CollisionCheckRange = 30f;

    // Minimum distance to separate ships
    private const float MinimumSafeDistance = 50f;

    // Maximum attempts to find a safe position
    private const int MaxRepositionAttempts = 10;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<DockingComponent> _dockingQuery;

    public override void Initialize()
    {
        base.Initialize();

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _dockingQuery = GetEntityQuery<DockingComponent>();

        // Subscribe to FTL events to handle collision prevention
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
    }

    /// <summary>
    /// Called when a ship completes FTL travel to check for potential collisions
    /// </summary>
    private void OnFTLCompleted(ref FTLCompletedEvent ev)
    {
        var shuttle = ev.Entity;
        var mapUid = ev.MapUid;

        if (!_xformQuery.TryGetComponent(shuttle, out var xform) ||
            !_physicsQuery.TryGetComponent(shuttle, out var physics) ||
            !_gridQuery.TryGetComponent(shuttle, out var grid))
            return;

        var mapId = xform.MapID;

        // Don't process if this is a linked shuttle (those will be moved with their main shuttle)
        if (TryComp<FTLComponent>(shuttle, out var ftlComp) && ftlComp.LinkedShuttle.HasValue)
            return;

        // Get all docked ships to this shuttle to ignore them in collision checks
        var dockedShips = new HashSet<EntityUid>();
        _shuttle.GetAllDockedShuttles(shuttle, dockedShips);

        // Check for nearby ships
        var shuttlePosition = _transform.GetWorldPosition(shuttle);
        var shuttleAABB = grid.LocalAABB.Translated(shuttlePosition);
        var range = shuttleAABB.MaxDimension + CollisionCheckRange;

        // Find nearby grids
        var nearbyGrids = new List<(EntityUid Entity, float Distance)>();
        foreach (var otherGrid in _mapManager.FindGridsIntersecting(mapId, new Box2(
            shuttlePosition - new Vector2(range, range),
            shuttlePosition + new Vector2(range, range))))
        {
            // Skip self
            if (otherGrid.Owner == shuttle)
                continue;

            // Skip ships that are docked to this shuttle
            if (dockedShips.Contains(otherGrid.Owner))
                continue;

            // Check if this grid is docked to any other grids that are docked to our shuttle
            bool isIndirectlyDocked = false;
            foreach (var dockedShip in dockedShips)
            {
                var otherDockedShips = new HashSet<EntityUid>();
                _shuttle.GetAllDockedShuttles(dockedShip, otherDockedShips);
                if (otherDockedShips.Contains(otherGrid.Owner))
                {
                    isIndirectlyDocked = true;
                    break;
                }
            }

            if (isIndirectlyDocked)
                continue;

            // Only care about grids with physics (actual ships)
            if (!_physicsQuery.TryGetComponent(otherGrid.Owner, out var otherPhysics) ||
                !_xformQuery.TryGetComponent(otherGrid.Owner, out var otherXform))
                continue;

            var otherPos = _transform.GetWorldPosition(otherGrid.Owner);
            var distance = (otherPos - shuttlePosition).Length();

            // If too close, add to the list for potential repositioning
            if (distance < MinimumSafeDistance)
            {
                nearbyGrids.Add((otherGrid.Owner, distance));
            }
        }

        // If no nearby grids, no need to reposition
        if (nearbyGrids.Count == 0)
            return;

        // Sort by distance (closest first)
        nearbyGrids.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        // Try to find a safe position away from other ships
        var newPosition = FindSafePosition(shuttle, mapId, shuttlePosition, shuttleAABB.Size.X, dockedShips);

        if (newPosition != shuttlePosition)
        {
            // Move the ship to the new position
            _transform.SetWorldPosition(shuttle, newPosition);

            // Log the repositioning
            Log.Info($"FTL Anti-Collision: Repositioned ship {ToPrettyString(shuttle)} to prevent collision");
        }
    }

    /// <summary>
    /// Find a safe position away from other ships by testing multiple positions
    /// at increasing distances in random directions.
    /// </summary>
    /// <param name="shuttle">The shuttle entity to reposition</param>
    /// <param name="mapId">The map ID where the shuttle is located</param>
    /// <param name="originalPosition">The original position of the shuttle</param>
    /// <param name="shipSize">The approximate size of the shuttle</param>
    /// <param name="dockedShips">Ships docked to this shuttle to ignore in collision checks</param>
    /// <returns>A new safe position, or the original position if no safe position could be found</returns>
    private Vector2 FindSafePosition(EntityUid shuttle, MapId mapId, Vector2 originalPosition, float shipSize, HashSet<EntityUid> dockedShips)
    {
        // Try a few random directions at increasing distances
        for (int attempt = 0; attempt < MaxRepositionAttempts; attempt++)
        {
            // Increase distance with each attempt
            var distance = MinimumSafeDistance + (attempt * 15f);

            // Pick a random direction
            var angle = _random.NextAngle();
            var offset = angle.ToVec() * distance;

            var testPosition = originalPosition + offset;

            // Check if this position is clear
            if (IsPositionClear(shuttle, mapId, testPosition, shipSize, dockedShips))
            {
                return testPosition;
            }
        }

        // If all attempts failed, try one more time with a much larger distance
        var lastResortDistance = MinimumSafeDistance + (MaxRepositionAttempts * 30f);
        var lastResortAngle = _random.NextAngle();
        var lastResortOffset = lastResortAngle.ToVec() * lastResortDistance;

        return originalPosition + lastResortOffset;
    }

    /// <summary>
    /// Check if a position is clear of other ships by looking for grids in the area
    /// </summary>
    /// <param name="shuttle">The shuttle entity to check for</param>
    /// <param name="mapId">The map ID where the shuttle is located</param>
    /// <param name="position">The position to check</param>
    /// <param name="shipSize">The approximate size of the shuttle</param>
    /// <param name="dockedShips">Ships docked to this shuttle to ignore in collision checks</param>
    /// <returns>True if the position is clear, false otherwise</returns>
    private bool IsPositionClear(EntityUid shuttle, MapId mapId, Vector2 position, float shipSize, HashSet<EntityUid> dockedShips)
    {
        // Buffer around the ship
        var checkSize = shipSize + MinimumSafeDistance;

        // Check for grids in the area
        foreach (var otherGrid in _mapManager.FindGridsIntersecting(mapId, new Box2(
            position - new Vector2(checkSize, checkSize),
            position + new Vector2(checkSize, checkSize))))
        {
            // Skip self
            if (otherGrid.Owner == shuttle)
                continue;

            // Skip ships that are docked to this shuttle
            if (dockedShips.Contains(otherGrid.Owner))
                continue;

            // Check if this grid is docked to any other grids that are docked to our shuttle
            bool isIndirectlyDocked = false;
            foreach (var dockedShip in dockedShips)
            {
                var otherDockedShips = new HashSet<EntityUid>();
                _shuttle.GetAllDockedShuttles(dockedShip, otherDockedShips);
                if (otherDockedShips.Contains(otherGrid.Owner))
                {
                    isIndirectlyDocked = true;
                    break;
                }
            }

            if (isIndirectlyDocked)
                continue;

            // If we found another grid, position is not clear
            if (_physicsQuery.HasComponent(otherGrid.Owner))
                return false;
        }

        return true;
    }
}
