using System.Linq;
using System.Numerics;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Content.Shared.Shuttles.Components; // Frontier

namespace Content.Server.Shuttles.Systems;

public sealed partial class DockingSystem
{
    /*
     * Handles the shuttle side of FTL docking.
     */

    private const int DockRoundingDigits = 2;

    public Angle GetAngle(EntityUid uid, TransformComponent xform, EntityUid targetUid, TransformComponent targetXform)
    {
        var (shuttlePos, shuttleRot) = _transform.GetWorldPositionRotation(xform);
        var (targetPos, targetRot) = _transform.GetWorldPositionRotation(targetXform);

        var shuttleCOM = Robust.Shared.Physics.Transform.Mul(new Transform(shuttlePos, shuttleRot),
            _physicsQuery.GetComponent(uid).LocalCenter);
        var targetCOM = Robust.Shared.Physics.Transform.Mul(new Transform(targetPos, targetRot),
            _physicsQuery.GetComponent(targetUid).LocalCenter);

        var mapDiff = shuttleCOM - targetCOM;
        var angle = mapDiff.ToWorldAngle();
        angle -= targetRot;
        return angle;
    }

    /// <summary>
    /// Checks if 2 docks can be connected by moving the shuttle directly onto docks.
    /// </summary>
    private bool CanDock(
        DockingComponent shuttleDock,
        TransformComponent shuttleDockXform,
        DockingComponent gridDock,
        TransformComponent gridDockXform,
        Box2 shuttleAABB,
        Angle targetGridRotation,
        FixturesComponent shuttleFixtures,
        Entity<MapGridComponent> gridEntity,
        bool isMap,
        out Matrix3x2 matty,
        out Box2 shuttleDockedAABB,
        out Angle gridRotation)
    {
        shuttleDockedAABB = Box2.UnitCentered;
        gridRotation = Angle.Zero;
        matty = Matrix3x2.Identity;

        if (shuttleDock.Docked ||
            gridDock.Docked ||
            !shuttleDockXform.Anchored ||
            !gridDockXform.Anchored)
        {
            return false;
        }

        // Frontier: check dock types
        if ((shuttleDock.DockType & gridDock.DockType) == DockType.None)
            return false;
        // End Frontier

        // First, get the station dock's position relative to the shuttle, this is where we rotate it around
        var stationDockPos = shuttleDockXform.LocalPosition +
                             shuttleDockXform.LocalRotation.RotateVec(new Vector2(0f, -1f));

        // Need to invert the grid's angle.
        var shuttleDockAngle = shuttleDockXform.LocalRotation;
        var gridDockAngle = gridDockXform.LocalRotation.Opposite();
        var offsetAngle = gridDockAngle - shuttleDockAngle;

        var stationDockMatrix = Matrix3Helpers.CreateInverseTransform(stationDockPos, shuttleDockAngle);
        var gridXformMatrix = Matrix3Helpers.CreateTransform(gridDockXform.LocalPosition, gridDockAngle);
        matty = Matrix3x2.Multiply(stationDockMatrix, gridXformMatrix);

        if (!ValidSpawn(gridEntity, matty, offsetAngle, shuttleFixtures, isMap))
            return false;

        shuttleDockedAABB = matty.TransformBox(shuttleAABB);
        gridRotation = offsetAngle.Reduced();
        return true;
    }

    /// <summary>
    /// Gets docking config between 2 specific docks.
    /// </summary>
    public DockingConfig? GetDockingConfig(
        EntityUid shuttleUid,
        EntityUid targetGrid,
        EntityUid shuttleDockUid,
        DockingComponent shuttleDock,
        EntityUid gridDockUid,
        DockingComponent gridDock)
    {
        var shuttleDocks = new List<Entity<DockingComponent>>(1)
       {
           (shuttleDockUid, shuttleDock)
       };

        var gridDocks = new List<Entity<DockingComponent>>(1)
       {
           (gridDockUid, gridDock)
       };

        return GetDockingConfigPrivate(shuttleUid, targetGrid, shuttleDocks, gridDocks);
    }

    /// <summary>
    /// Tries to get a valid docking configuration for the shuttle to the target grid.
    /// </summary>
    /// <param name="priorityTag">Priority docking tag to prefer, e.g. for emergency shuttle</param>
    public DockingConfig? GetDockingConfig(EntityUid shuttleUid, EntityUid targetGrid, string? priorityTag = null, DockType dockType = DockType.Airlock) // Frontier: add dockType
    {
        var gridDocks = GetDocks(targetGrid);
        var shuttleDocks = GetDocks(shuttleUid);

        return GetDockingConfigPrivate(shuttleUid, targetGrid, shuttleDocks, gridDocks, priorityTag, dockType); // Frontier: add dockType
    }

    /// <summary>
    /// Tries to get a docking config at the specified coordinates and angle.
    /// </summary>
    public DockingConfig? GetDockingConfigAt(EntityUid shuttleUid,
        EntityUid targetGrid,
        EntityCoordinates coordinates,
        Angle angle,
        bool fallback = true,
        DockType dockType = DockType.Airlock) // Frontier
    {
        var gridDocks = GetDocks(targetGrid);
        var shuttleDocks = GetDocks(shuttleUid);

        var configs = GetDockingConfigs(shuttleUid, targetGrid, shuttleDocks, gridDocks, dockType); // Frontier: add dockType

        foreach (var config in configs)
        {
            if (config.Coordinates.Equals(coordinates) && config.Angle.EqualsApprox(angle, 0.15))
            {
                return config;
            }
        }

        if (fallback && configs.Count > 0)
        {
            return configs.First();
        }

        return null;
    }

    /// <summary>
    /// Gets all docking configs between the 2 grids.
    /// </summary>
    private List<DockingConfig> GetDockingConfigs(
        EntityUid shuttleUid,
        EntityUid targetGrid,
        List<Entity<DockingComponent>> shuttleDocks,
        List<Entity<DockingComponent>> gridDocks,
        DockType dockType) // Frontier: add dockType
    {
        var validDockConfigs = new List<DockingConfig>();

        if (gridDocks.Count <= 0)
            return validDockConfigs;

        var targetGridGrid = _gridQuery.GetComponent(targetGrid);
        var targetGridXform = _xformQuery.GetComponent(targetGrid);
        var targetGridAngle = _transform.GetWorldRotation(targetGridXform).Reduced();
        var shuttleFixturesComp = Comp<FixturesComponent>(shuttleUid);
        var shuttleAABB = _gridQuery.GetComponent(shuttleUid).LocalAABB;

        var isMap = HasComp<MapComponent>(targetGrid);

        var dockedShuttles = new HashSet<EntityUid>();
        if (targetGridXform.MapUid is { } targetMapUid && HasComp<SalvageExpeditionComponent>(targetMapUid))
            dockedShuttles.Add(shuttleUid);
        else
            GetFTLDockedShuttlesForDocking(shuttleUid, dockedShuttles);

        var dockedInfos = new List<(EntityUid Uid, Matrix3x2 RelMatrix, Angle RelRot, FixturesComponent Fixtures, Box2 LocalAABB)>();
        if (dockedShuttles.Count > 1)
        {
            var (mainPos, mainRot) = _transform.GetWorldPositionRotation(shuttleUid);
            foreach (var dockedUid in dockedShuttles)
            {
                if (dockedUid == shuttleUid)
                    continue;

                if (!TryComp<FixturesComponent>(dockedUid, out var dockedFixtures) ||
                    !_gridQuery.TryGetComponent(dockedUid, out var dockedGridComp) ||
                    !_xformQuery.TryGetComponent(dockedUid, out var dockedXform))
                {
                    continue;
                }

                var (dockedPos, dockedRot) = _transform.GetWorldPositionRotation(dockedXform);
                var relPos = (-mainRot).RotateVec(dockedPos - mainPos);
                var relRot = (dockedRot - mainRot).Reduced();
                var relMatrix = Matrix3Helpers.CreateTransform(relPos, relRot);
                dockedInfos.Add((dockedUid, relMatrix, relRot, dockedFixtures, dockedGridComp.LocalAABB));
            }
        }

        var grids = new List<Entity<MapGridComponent>>();
        if (shuttleDocks.Count > 0)
        {
            // We'll try all combinations of shuttle docks and see which one is most suitable
            foreach (var (dockUid, shuttleDock) in shuttleDocks)
            {
                var shuttleDockXform = _xformQuery.GetComponent(dockUid);

                // Frontier: skip docks that don't match type
                if ((shuttleDock.DockType & dockType) == DockType.None)
                    continue;
                // End Frontier

                foreach (var (gridDockUid, gridDock) in gridDocks)
                {
                    var gridXform = _xformQuery.GetComponent(gridDockUid);

                    // Frontier: skip docks that don't match type
                    if ((gridDock.DockType & dockType) == DockType.None)
                        continue;
                    // End Frontier

                    if (!CanDock(
                            shuttleDock, shuttleDockXform,
                            gridDock, gridXform,
                            shuttleAABB,
                            targetGridAngle,
                            shuttleFixturesComp,
                            (targetGrid, targetGridGrid),
                            isMap,
                            out var matty,
                            out var dockedAABB,
                            out var targetAngle))
                    {
                        continue;
                    }

                    // Can't just use the AABB as we want to get bounds as tight as possible.
                    var gridPosition = new EntityCoordinates(targetGrid, Vector2.Transform(Vector2.Zero, matty));
                    var spawnPosition = new EntityCoordinates(targetGridXform.MapUid!.Value, _transform.ToMapCoordinates(gridPosition).Position);

                    // TODO: use tight bounds
                    var targetWorldAngle = (targetGridAngle + targetAngle).Reduced(); // Frontier
                    var dockedBounds = new Box2Rotated(shuttleAABB.Translated(spawnPosition.Position), targetWorldAngle, spawnPosition.Position); // Frontier: targetAngle<targetWorldAngle

                    // Check if there's no intersecting grids (AKA oh god it's docking at cargo).
                    grids.Clear();
                    _mapManager.FindGridsIntersecting(targetGridXform.MapID, dockedBounds, ref grids, includeMap: false);
                    if (grids.Any(o => o.Owner != targetGrid && o.Owner != targetGridXform.MapUid && !dockedShuttles.Contains(o.Owner)))
                    {
                        continue;
                    }

                    // Also ensure every FTL-locked docked shuttle has clearance at this docking pose.
                    var dockedCollision = false;
                    foreach (var docked in dockedInfos)
                    {
                        var dockedMatty = Matrix3x2.Multiply(docked.RelMatrix, matty);
                        var dockedTargetAngle = (targetAngle + docked.RelRot).Reduced();

                        if (!ValidSpawn((targetGrid, targetGridGrid), dockedMatty, dockedTargetAngle, docked.Fixtures, isMap))
                        {
                            dockedCollision = true;
                            break;
                        }

                        var dockedGridPos = new EntityCoordinates(targetGrid, Vector2.Transform(Vector2.Zero, dockedMatty));
                        var dockedSpawnPos = _transform.ToMapCoordinates(dockedGridPos).Position;
                        var dockedWorldAngle = (targetGridAngle + dockedTargetAngle).Reduced();
                        var dockedOtherBounds = new Box2Rotated(docked.LocalAABB.Translated(dockedSpawnPos), dockedWorldAngle, dockedSpawnPos);

                        grids.Clear();
                        _mapManager.FindGridsIntersecting(targetGridXform.MapID, dockedOtherBounds, ref grids, includeMap: false);
                        if (grids.Any(o => o.Owner != targetGrid && o.Owner != targetGridXform.MapUid && !dockedShuttles.Contains(o.Owner)))
                        {
                            dockedCollision = true;
                            break;
                        }
                    }

                    if (dockedCollision)
                    {
                        continue;
                    }

                    // Alright well the spawn is valid now to check how many we can connect
                    // Get the matrix for each shuttle dock and test it against the grid docks to see
                    // if the connected position / direction matches.

                    var dockedPorts = new List<(EntityUid DockAUid, EntityUid DockBUid, DockingComponent DockA, DockingComponent DockB)>()
                   {
                       (dockUid, gridDockUid, shuttleDock, gridDock),
                   };

                    dockedAABB = dockedAABB.Rounded(DockRoundingDigits);

                    foreach (var (otherUid, other) in shuttleDocks)
                    {
                        if (other == shuttleDock)
                            continue;

                        // Frontier: skip docks that don't match type
                        if ((other.DockType & dockType) == DockType.None)
                            continue;
                        // End Frontier

                        foreach (var (otherGridUid, otherGrid) in gridDocks)
                        {
                            if (otherGrid == gridDock)
                                continue;

                            // Frontier: skip docks that don't match type
                            if ((otherGrid.DockType & dockType) == DockType.None)
                                continue;
                            // End Frontier

                            if (!CanDock(
                                    other,
                                    _xformQuery.GetComponent(otherUid),
                                    otherGrid,
                                    _xformQuery.GetComponent(otherGridUid),
                                    shuttleAABB,
                                    targetGridAngle,
                                    shuttleFixturesComp,
                                    (targetGrid, targetGridGrid),
                                    isMap,
                                    out _,
                                    out var otherdockedAABB,
                                    out var otherTargetAngle))
                            {
                                continue;
                            }

                            otherdockedAABB = otherdockedAABB.Rounded(DockRoundingDigits);

                            // Different setup.
                            if (!targetAngle.Equals(otherTargetAngle) ||
                                !dockedAABB.Equals(otherdockedAABB))
                            {
                                continue;
                            }

                            dockedPorts.Add((otherUid, otherGridUid, other, otherGrid));
                        }
                    }

                    validDockConfigs.Add(new DockingConfig()
                    {
                        Docks = dockedPorts,
                        Coordinates = gridPosition,
                        Area = dockedAABB,
                        Angle = targetAngle,
                    });
                }
            }
        }

        return validDockConfigs;
    }

    private DockingConfig? GetDockingConfigPrivate(
        EntityUid shuttleUid,
        EntityUid targetGrid,
        List<Entity<DockingComponent>> shuttleDocks,
        List<Entity<DockingComponent>> gridDocks,
        string? priorityTag = null,
        DockType dockType = DockType.Airlock) // Frontier
    {
        var validDockConfigs = GetDockingConfigs(shuttleUid, targetGrid, shuttleDocks, gridDocks, dockType); // Frontier: add dockType

        if (validDockConfigs.Count <= 0)
            return null;

        var targetGridAngle = _transform.GetWorldRotation(targetGrid).Reduced();

        // Prioritise by priority docks, then by maximum connected ports, then by most similar angle.
        validDockConfigs = validDockConfigs
           .OrderByDescending(x => IsConfigPriority(x, priorityTag))
           .ThenByDescending(x => x.Docks.Count)
           .ThenBy(x => Math.Abs(Angle.ShortestDistance(x.Angle.Reduced(), targetGridAngle).Theta)).ToList();

        var location = validDockConfigs.First();
        location.TargetGrid = targetGrid;
        // TODO: Ideally do a hyperspace warpin, just have it run on like a 10 second timer.

        return location;
    }

    public bool IsConfigPriority(DockingConfig config, string? priorityTag)
    {
        return config.Docks.Any(docks =>
            TryComp<PriorityDockComponent>(docks.DockBUid, out var priority)
            && priority.Tag?.Equals(priorityTag) == true);
    }

    /// <summary>
    /// Checks whether the shuttle can warp to the specified position.
    /// </summary>
    private bool ValidSpawn(Entity<MapGridComponent> gridEntity, Matrix3x2 matty, Angle angle, FixturesComponent shuttleFixturesComp, bool isMap)
    {
        var transform = new Transform(Vector2.Transform(Vector2.Zero, matty), angle);

        // Because some docking bounds are tight af need to check each chunk individually
        foreach (var fix in shuttleFixturesComp.Fixtures.Values)
        {
            var polyShape = (PolygonShape)fix.Shape;
            var aabb = polyShape.ComputeAABB(transform, 0);
            aabb = aabb.Enlarged(-0.01f);

            // If it's a map check no hard collidable anchored entities overlap
            if (isMap)
            {
                var localTiles = _mapSystem.GetLocalTilesEnumerator(gridEntity.Owner, gridEntity.Comp, aabb);

                while (localTiles.MoveNext(out var tile))
                {
                    var anchoredEnumerator = _mapSystem.GetAnchoredEntitiesEnumerator(gridEntity.Owner, gridEntity.Comp, tile.GridIndices);

                    while (anchoredEnumerator.MoveNext(out var anc))
                    {
                        if (!_physicsQuery.TryGetComponent(anc, out var physics) ||
                            !physics.CanCollide ||
                            !physics.Hard)
                        {
                            continue;
                        }

                        return false;
                    }
                }
            }
            // If it's not a map check it doesn't overlap the grid.
            else
            {
                if (_mapSystem.GetLocalTilesIntersecting(gridEntity.Owner, gridEntity.Comp, aabb).Any())
                    return false;
            }
        }

        return true;
    }

    public List<Entity<DockingComponent>> GetDocks(EntityUid uid)
    {
        _dockingSet.Clear();
        _lookup.GetChildEntities(uid, _dockingSet);

        return _dockingSet.ToList();
    }

    /// <summary>
    /// Collects all FTL-locked shuttles docked to <paramref name="shuttleUid"/> without mutating components.
    /// </summary>
    private void GetFTLDockedShuttlesForDocking(EntityUid shuttleUid, HashSet<EntityUid> dockedShuttles)
    {
        if (HasComp<EmergencyShuttleComponent>(shuttleUid) || HasComp<ArrivalsShuttleComponent>(shuttleUid))
        {
            dockedShuttles.Add(shuttleUid);
            return;
        }

        CollectFTLDockedShuttlesForDocking(shuttleUid, dockedShuttles);
    }

    private void CollectFTLDockedShuttlesForDocking(EntityUid shuttleUid, HashSet<EntityUid> dockedShuttles)
    {
        if (!dockedShuttles.Add(shuttleUid))
            return;

        var docks = GetDocks(shuttleUid);
        foreach (var dock in docks)
        {
            if (!TryComp<DockingComponent>(dock, out var dockComp) || !dockComp.Docked || dockComp.DockedWith == null)
                continue;

            var dockedGridUid = _transform.GetParentUid(dockComp.DockedWith.Value);
            if (dockedGridUid == EntityUid.Invalid || !HasComp<ShuttleComponent>(dockedGridUid))
                continue;

            if (TryComp<FTLLockComponent>(dockedGridUid, out var ftlLock) && !ftlLock.Enabled)
            {
                var nestedDocks = GetDocks(dockedGridUid);
                foreach (var nestedDock in nestedDocks)
                {
                    if (!TryComp<DockingComponent>(nestedDock, out var nestedDockComp) ||
                        !nestedDockComp.Docked ||
                        nestedDockComp.DockedWith == null)
                        continue;

                    var nestedDockedGridUid = _transform.GetParentUid(nestedDockComp.DockedWith.Value);
                    if (nestedDockedGridUid == EntityUid.Invalid ||
                        nestedDockedGridUid == shuttleUid ||
                        !HasComp<ShuttleComponent>(nestedDockedGridUid))
                        continue;

                    if (!TryComp<FTLLockComponent>(nestedDockedGridUid, out var nestedFtlLock) || nestedFtlLock.Enabled)
                    {
                        CollectFTLDockedShuttlesForDocking(nestedDockedGridUid, dockedShuttles);
                    }
                }

                continue;
            }

            if (!dockedShuttles.Contains(dockedGridUid))
            {
                CollectFTLDockedShuttlesForDocking(dockedGridUid, dockedShuttles);
            }
        }
    }

    /// <summary>
    /// Mono: Checks if two grids are docked together by examining if any docking port on gridA is connected to any docking port on gridB.
    /// </summary>
    public bool AreGridsDocked(EntityUid gridA, EntityUid gridB)
    {
        var docksA = GetDocks(gridA);

        foreach (var dockA in docksA)
        {
            if (!dockA.Comp.Docked || dockA.Comp.DockedWith == null)
                continue;

            // Get the grid that this dock is connected to
            var connectedDockGrid = Transform(dockA.Comp.DockedWith.Value).GridUid;
            if (connectedDockGrid == gridB)
                return true;
        }

        return false;
    }
}
