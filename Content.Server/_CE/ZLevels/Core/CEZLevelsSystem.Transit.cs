/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private DockingSystem _dockSystem = default!;
    [Dependency] private ShuttleConsoleSystem _console = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;

    private void InitializeTransit()
    {
        SubscribeLocalEvent<GridAddEvent>(OnGridAdd);
        SubscribeLocalEvent<MapGridComponent, EntParentChangedMessage>(OnGridParentChanged);
        SubscribeLocalEvent<CEZTransitMapComponent, ComponentShutdown>(OnTransitMapShutdown);
    }

    /// <summary>
    /// A convoy layer map is going away: unlink its neighbours so the stack doesn't
    /// dangle, and promote a survivor to lead if the departing layer was driving.
    /// </summary>
    private void OnTransitMapShutdown(Entity<CEZTransitMapComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.TransitBelow is { } below && TryComp<CEZTransitMapComponent>(below, out var belowComp))
        {
            belowComp.TransitAbove = null;
            if (ent.Comp.ConvoyLead)
                belowComp.ConvoyLead = true;
            Dirty(below, belowComp);
        }

        if (ent.Comp.TransitAbove is { } above && TryComp<CEZTransitMapComponent>(above, out var aboveComp))
        {
            aboveComp.TransitBelow = null;
            if (ent.Comp.ConvoyLead && ent.Comp.TransitBelow == null)
                aboveComp.ConvoyLead = true;
            Dirty(above, aboveComp);
        }
    }

    private void OnGridAdd(GridAddEvent ev)
    {
        RefreshGridZPhysics(ev.EntityUid);
    }

    private void OnGridParentChanged(Entity<MapGridComponent> ent, ref EntParentChangedMessage args)
    {
        RefreshGridZPhysics(ent);
    }

    /// <summary>
    /// Maps can join a z-network after their grids already loaded; those grids never saw a
    /// parent change, so sweep them for z-physics/faller state when the map joins.
    /// </summary>
    private void SweepMapGridsForZPhysics(EntityUid mapUid)
    {
        var children = Transform(mapUid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<MapGridComponent>(child))
                RefreshGridZPhysics(child);
        }
    }

    /// <summary>
    /// Grids arriving on a z-network get z-physics and a gravity grace period; grids
    /// leaving it drop the gravity state so a return starts a fresh grace.
    /// </summary>
    private void RefreshGridZPhysics(EntityUid grid)
    {
        if (HasComp<MapComponent>(grid) || TerminatingOrDeleted(grid))
            return;

        var mapUid = Transform(grid).MapUid;
        var onZLevels = HasComp<CEZMapComponent>(mapUid) || HasComp<CEZTransitMapComponent>(mapUid);

        if (!onZLevels)
        {
            RemComp<CEZGridFallerComponent>(grid);
            return;
        }

        EnsureComp<CEZPhysicsComponent>(grid);

        if (!HasComp<CEZGridFallerComponent>(grid))
        {
            var faller = AddComp<CEZGridFallerComponent>(grid);
            faller.GravityTime = _timing.CurTime + TimeSpan.FromSeconds(faller.GridGravityGraceSeconds);
        }
    }

    /// <inheritdoc/>
    protected override bool TryMoveGrid(Entity<MapGridComponent> grid,
        Entity<CEZMapComponent, MapComponent> targetMap,
        int offset)
    {
        var movedGrids = CollectGridSet(grid);
        MoveGridSetToMap(movedGrids, targetMap.Owner, offset, targetMap.Comp1.Depth);

        foreach (var gridUid in movedGrids)
        {
            // Ships parked on the ground get their engines back once they're off the
            // ground layer (see CEZGroundLayerComponent).
            if (!HasComp<CEZGroundLayerComponent>(targetMap.Owner))
                _shuttle.Enable(gridUid);
        }

        return true;
    }

    /// <summary>
    /// Collects the grid and everything docked to it, dropping members that aren't on
    /// the same map (docked chains can span maps in degenerate cases).
    /// </summary>
    private HashSet<EntityUid> CollectGridSet(EntityUid grid)
    {
        var sourceMap = Transform(grid).MapUid;

        var movedGrids = new HashSet<EntityUid>();
        _shuttle.GetAllDockedShuttles(grid, movedGrids);
        movedGrids.Add(grid);
        movedGrids.RemoveWhere(uid => Transform(uid).MapUid != sourceMap);

        return movedGrids;
    }

    /// <summary>
    /// One z-level's worth of a convoy about to enter transit: the source map, its
    /// depth, the grid that will define the layer's transit map progress, and every
    /// grid moving with it.
    /// </summary>
    private readonly record struct CEConvoyEntryLayer(EntityUid SourceMap, int Depth, EntityUid Primary, HashSet<EntityUid> Grids);

    /// <summary>
    /// Groups the driving grid's z-network (plus every member's docked set) into
    /// layers by source z-level, ordered bottom-up. A grid without a network yields
    /// a single layer — identical to the old docked-set behaviour.
    /// </summary>
    private List<CEConvoyEntryLayer> CollectConvoyLayers(Entity<MapGridComponent> grid)
    {
        var layers = new Dictionary<EntityUid, CEConvoyEntryLayer>();

        void AddMember(EntityUid member)
        {
            if (Transform(member).MapUid is not { } map || !TryComp<CEZMapComponent>(map, out var zMap))
                return;

            if (!layers.TryGetValue(map, out var layer))
                layers[map] = layer = new CEConvoyEntryLayer(map, zMap.Depth, member, new HashSet<EntityUid>());

            layer.Grids.UnionWith(CollectGridSet(member));

            // The driving grid always fronts its own layer.
            if (member == grid.Owner)
                layers[map] = layer with { Primary = member };
        }

        if (TryGetGridNetwork(grid, out var network))
        {
            foreach (var member in network.Comp.Grids)
                AddMember(member);
        }

        // Ensures the driving grid's layer exists even without a network, and claims
        // layer-primary for it when it does have one.
        AddMember(grid);

        var result = new List<CEConvoyEntryLayer>(layers.Values);
        result.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        return result;
    }

    /// <summary>
    /// Every transit map in the convoy containing <paramref name="transitMap"/>,
    /// ordered bottom-up. A lone transit map yields a single-element list.
    /// </summary>
    private List<EntityUid> GetConvoyMaps(EntityUid transitMap)
    {
        var maps = new List<EntityUid> { transitMap };
        var seen = new HashSet<EntityUid> { transitMap };

        var cursor = transitMap;
        while (TryComp<CEZTransitMapComponent>(cursor, out var t)
               && t.TransitBelow is { } below
               && !TerminatingOrDeleted(below)
               && seen.Add(below))
        {
            maps.Insert(0, below);
            cursor = below;
        }

        cursor = transitMap;
        while (TryComp<CEZTransitMapComponent>(cursor, out var t)
               && t.TransitAbove is { } above
               && !TerminatingOrDeleted(above)
               && seen.Add(above))
        {
            maps.Add(above);
            cursor = above;
        }

        return maps;
    }

    /// <summary>Every grid currently on a map (a transit map hosts exactly one set).</summary>
    private HashSet<EntityUid> CollectGridsOnMap(EntityUid mapUid)
    {
        var grids = new HashSet<EntityUid>();
        var children = Transform(mapUid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (_mapGridQuery.HasComp(child))
                grids.Add(child);
        }

        return grids;
    }

    /// <summary>
    /// The full set a grid moves with: every grid on every layer of its transit
    /// convoy when in convoy transit, otherwise its same-map docked set.
    /// </summary>
    public HashSet<EntityUid> CollectTransitSet(EntityUid grid)
    {
        if (Transform(grid).MapUid is { } map
            && TryComp<CEZTransitMapComponent>(map, out var transit)
            && (transit.TransitAbove != null || transit.TransitBelow != null))
        {
            var set = new HashSet<EntityUid>();
            foreach (var convoyMap in GetConvoyMaps(map))
                set.UnionWith(CollectGridsOnMap(convoyMap));
            return set;
        }

        return CollectGridSet(grid);
    }

    /// <summary>
    /// Re-checks every transit convoy against current z-grid-network membership and splits
    /// any stack whose layers are no longer held together — a connector destroyed mid-flight,
    /// a hull tile blown out. Each severed fragment gets its own lead and falls independently
    /// from there (its followers already track the lead's velocity, so the split doesn't reset
    /// their speed). Called by <see cref="CEZGridConnectorSystem"/> after it reconciles networks.
    /// </summary>
    public void RevalidateTransitConvoys()
    {
        // 1. Sever the link between any two stacked transit layers whose grids no longer
        //    share a z-grid network — the connector that bridged them is gone.
        var query = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (query.MoveNext(out var uid, out var transit))
        {
            if (transit.TransitBelow is not { } below ||
                TerminatingOrDeleted(below) ||
                !TryComp<CEZTransitMapComponent>(below, out var belowComp))
            {
                continue;
            }

            if (SameConvoyNetwork(uid, below))
                continue;

            transit.TransitBelow = null;
            belowComp.TransitAbove = null;
            Dirty(uid, transit);
            Dirty(below, belowComp);
        }

        // 2. Every resulting sub-stack needs exactly one lead to drive its fall.
        var leadQuery = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (leadQuery.MoveNext(out var uid, out var transit))
        {
            // Walk each stack once, from its bottom (the layer with nothing linked below).
            if (transit.TransitBelow == null)
                EnsureConvoyLead(uid);
        }
    }

    /// <summary>Whether the grid layers on two transit maps are still in the same z-grid network.</summary>
    private bool SameConvoyNetwork(EntityUid mapA, EntityUid mapB)
    {
        if (!TryGetAnyGrid(mapA, out var gridA) || !TryGetAnyGrid(mapB, out var gridB))
            return false; // a layer with no grids can't hold the stack together

        return TryGetGridNetwork(gridA, out var netA)
               && TryGetGridNetwork(gridB, out var netB)
               && netA.Owner == netB.Owner;
    }

    private bool TryGetAnyGrid(EntityUid mapUid, out EntityUid grid)
    {
        var children = Transform(mapUid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (_mapGridQuery.HasComp(child))
            {
                grid = child;
                return true;
            }
        }

        grid = default;
        return false;
    }

    /// <summary>Ensures the transit sub-stack rooted at <paramref name="bottomMap"/> has exactly one lead.</summary>
    private void EnsureConvoyLead(EntityUid bottomMap)
    {
        var maps = GetConvoyMaps(bottomMap);

        var haveLead = false;
        foreach (var m in maps)
        {
            if (!TryComp<CEZTransitMapComponent>(m, out var t) || !t.ConvoyLead)
                continue;

            if (!haveLead)
            {
                haveLead = true; // keep the first
            }
            else
            {
                t.ConvoyLead = false; // a split can strand two leads in one stack — demote extras
                Dirty(m, t);
            }
        }

        if (haveLead || maps.Count == 0)
            return;

        // No lead survived in this fragment — promote its bottom layer to drive the fall.
        if (TryComp<CEZTransitMapComponent>(maps[0], out var lead))
        {
            lead.ConvoyLead = true;
            Dirty(maps[0], lead);
        }
    }

    /// <summary>
    /// Moves a set of grids to another map, keeping world transforms (all z-level maps
    /// share one world coordinate space) and momentum, re-establishing the docking
    /// joints the engine clears on map changes, and notifying every passenger.
    /// </summary>
    private void MoveGridSetToMap(HashSet<EntityUid> movedGrids, EntityUid targetMap, int offset, int depth)
    {
        foreach (var gridUid in movedGrids)
        {
            var beforeEv = new CEZLevelBeforeMapMoveEvent(offset, depth);
            RaiseLocalEvent(gridUid, ref beforeEv);
        }

        foreach (var gridUid in movedGrids)
        {
            var xform = Transform(gridUid);
            var worldPos = _transform.GetWorldPosition(xform);
            var worldRot = _transform.GetWorldRotation(xform);

            // The map change wipes joints and can reset momentum, so save and restore it.
            var linVel = Vector2.Zero;
            var angVel = 0f;
            TryComp<PhysicsComponent>(gridUid, out var body);
            if (body != null)
            {
                linVel = body.LinearVelocity;
                angVel = body.AngularVelocity;
            }

            _transform.SetCoordinates(gridUid, xform, new EntityCoordinates(targetMap, worldPos), rotation: worldRot);

            if (body != null)
            {
                _physics.SetLinearVelocity(gridUid, linVel, body: body);
                _physics.SetAngularVelocity(gridUid, angVel, body: body);
            }
        }

        var ev = new CEZLevelMapMoveEvent(offset, depth);
        foreach (var gridUid in movedGrids)
        {
            _dockSystem.RedockDocks(gridUid);
            _console.RefreshShuttleConsoles(gridUid);

            RaiseLocalEvent(gridUid, ref ev);
            RaiseZMoveEventOnPassengers(gridUid, ref ev);
        }
    }

    /// <summary>
    /// Notifies everything riding the grid about the z-level change so cached values
    /// like <see cref="CEZPhysicsComponent.CurrentZLevel"/> stay correct.
    /// </summary>
    private void RaiseZMoveEventOnPassengers(EntityUid uid, ref CEZLevelMapMoveEvent ev)
    {
        var children = Transform(uid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (ZPhysicsQuery.HasComp(child))
                RaiseLocalEvent(child, ref ev);

            RaiseZMoveEventOnPassengers(child, ref ev);
        }
    }

    // ===== Transit maps: grids vertically between two z-levels =====

    /// <summary>
    /// Moves a grid (and its docked set) into a fresh transit map. Entry is
    /// direction-agnostic: a level's plane is both the top of the gap below and the
    /// bottom of the gap above, so the grid simply becomes airborne at its own plane
    /// (gap below at progress 1 when one exists, otherwise gap above at progress 0 —
    /// the same physical place). Which way it goes afterwards is just how its progress
    /// changes; crossing a plane hops gaps (see <see cref="SetTransitAltitude"/>).
    /// Pass <paramref name="preferUpperGap"/> to become airborne in the gap above
    /// instead (a liftoff shouldn't sweep the pad it's leaving when it climbs
    /// through its own plane).
    /// </summary>
    public bool TryEnterTransit(Entity<MapGridComponent> grid, float? startProgress = null, bool preferUpperGap = false)
    {
        var xform = Transform(grid);

        if (xform.MapUid is not { } currentMap)
            return false;

        if (HasComp<CEZTransitMapComponent>(currentMap))
            return false; // Already in transit.

        if (!HasComp<CEZMapComponent>(currentMap))
            return false;

        var layers = CollectConvoyLayers(grid);
        if (layers.Count == 0)
            return false;

        // Direction is decided for the whole convoy: descending needs a level below
        // the BOTTOM layer, climbing needs one above the TOP layer. Every interior
        // layer's gap is bounded by its neighbours' source maps, so only the two
        // extremes can fail.
        var hasBelow = TryMapDown(layers[0].SourceMap, out _);
        var hasAbove = TryMapUp(layers[^1].SourceMap, out _);

        var goDown = hasBelow && !(preferUpperGap && hasAbove);
        if (!goDown && !hasAbove)
            return false;

        var defaultProgress = goDown ? 1f : 0f;

        // Resolve every layer's gap up front — all-or-nothing, nothing moves on failure.
        var plans = new List<(CEConvoyEntryLayer Layer, EntityUid LowerMap, EntityUid UpperMap, int Offset, int Depth)>();
        foreach (var layer in layers)
        {
            if (goDown)
            {
                if (!TryMapDown(layer.SourceMap, out var layerBelow))
                    return false; // Non-consecutive stack — shouldn't happen with connector-built networks.

                plans.Add((layer, layerBelow.Owner, layer.SourceMap, -1, layerBelow.Comp.Depth));
            }
            else
            {
                if (!TryMapUp(layer.SourceMap, out var layerAbove))
                    return false;

                plans.Add((layer, layer.SourceMap, layerAbove.Owner, 1, layerAbove.Comp.Depth));
            }
        }

        var progress = Math.Clamp(startProgress ?? defaultProgress, 0f, 1f);

        // Create + link the transit stack bottom-up, then move each layer into its map.
        EntityUid? prevTransit = null;
        foreach (var plan in plans)
        {
            var transitMap = CreateTransitMap(plan.LowerMap, plan.UpperMap, plan.Layer.Primary);
            var transit = Comp<CEZTransitMapComponent>(transitMap);

            if (prevTransit is { } belowTransit)
            {
                transit.TransitBelow = belowTransit;
                var belowComp = Comp<CEZTransitMapComponent>(belowTransit);
                belowComp.TransitAbove = transitMap;
                Dirty(belowTransit, belowComp);
            }

            // The layer that initiated transit drives the whole convoy.
            transit.ConvoyLead = plan.Layer.Grids.Contains(grid.Owner);
            Dirty(transitMap, transit);

            MoveGridSetToMap(plan.Layer.Grids, transitMap, plan.Offset, plan.Depth);

            foreach (var gridUid in plan.Layer.Grids)
            {
                var zPhys = EnsureComp<CEZPhysicsComponent>(gridUid);
                SetZPosition((gridUid, zPhys), progress);

                // Everyone gets to watch, not just PVS neighbours.
                _pvsOverride.AddGlobalOverride(gridUid);

                // In transit = airborne: engines are live regardless of direction
                // (parked ships are Static and need the re-enable to move at all).
                _shuttle.Enable(gridUid);
            }

            prevTransit = transitMap;
        }

        return true;
    }

    /// <summary>
    /// Sets a transiting grid set's altitude in the z-network's depth coordinates:
    /// the integer part is a level, the fraction is the position in the gap above it
    /// (1.1 = a tenth of a gap above level 1). Absolute and idempotent — repeating the
    /// same value keeps the ship where it is, however many gaps it crossed to get
    /// there. Values below the bottom of the network land the set on the bottom level.
    /// </summary>
    public bool SetTransitAltitude(Entity<MapGridComponent> grid, float altitude)
    {
        var xform = Transform(grid);

        if (xform.MapUid is not { } transitMapUid ||
            !TryComp<CEZTransitMapComponent>(transitMapUid, out var transit))
        {
            return false;
        }

        if (transit.LowerMap is not { } anchor ||
            !TryComp<CEZMapComponent>(anchor, out var anchorZ))
        {
            return false;
        }

        // Altitude is absolute; the current gap starts at its lower level's depth.
        var progress = altitude - anchorZ.Depth;

        while (progress > 1f)
        {
            // The whole convoy hops together, so the TOP layer needs a level above
            // its gap; every interior layer's destination exists by construction.
            var convoy = GetConvoyMaps(transitMapUid);
            var topTransit = Comp<CEZTransitMapComponent>(convoy[^1]);

            if (topTransit.UpperMap is not { } topUpper)
            {
                progress = 1f;
                break;
            }

            // A ground layer overhead is a solid ceiling from below: a ship rises through a
            // hole in it but never punches through the ground itself. Mirror of the descent
            // rule (which lands ON ground, never past it). Clamp to the underside and stop.
            if (HasComp<CEZGroundLayerComponent>(topUpper) && ConvoyBlockedByCeiling(convoy[^1], topUpper))
            {
                progress = 1f;
                break;
            }

            if (!TryMapUp(topUpper, out _))
            {
                // Top of the network: give on-demand generation a chance to extend it
                // upward before clamping.
                RaiseExpandEvent(topUpper, up: true);

                if (!TryMapUp(topUpper, out _))
                {
                    progress = 1f;
                    break;
                }
            }

            if (!TryHopConvoyGap(convoy, ref transitMapUid, up: true))
            {
                progress = 1f;
                break;
            }

            progress -= 1f;
        }

        while (progress < 0f)
        {
            var convoy = GetConvoyMaps(transitMapUid);
            var bottomTransit = Comp<CEZTransitMapComponent>(convoy[0]);

            // Ground layers stop descents dead — you land ON them, never hop past
            // them. The BOTTOM layer decides for the whole convoy.
            if (bottomTransit.LowerMap is not { } bottomLower || HasComp<CEZGroundLayerComponent>(bottomLower))
                return LandTransitSet(grid);

            if (!TryMapDown(bottomLower, out _))
            {
                // Bottom of the network without ground under it: give on-demand
                // generation (procgen cave layers) a chance to extend it downward
                // before concluding there's nothing there.
                RaiseExpandEvent(bottomLower, up: false);

                if (!TryMapDown(bottomLower, out _))
                    return LandTransitSet(grid);
            }

            if (!TryHopConvoyGap(convoy, ref transitMapUid, up: false))
                return LandTransitSet(grid);

            progress += 1f;
        }

        foreach (var convoyMap in GetConvoyMaps(transitMapUid))
        {
            foreach (var gridUid in CollectGridsOnMap(convoyMap))
            {
                if (ZPhysicsQuery.TryComp(gridUid, out var zPhys))
                    SetZPosition((gridUid, zPhys), progress);
            }
        }

        return true;
    }

    /// <summary>
    /// Transit maps whose primary grid was deleted mid-gap (admin delete, crushed by
    /// another ship) have no exit path and would leak as phantom render passes.
    /// </summary>
    private void CleanupOrphanedTransitMaps()
    {
        var query = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (query.MoveNext(out var uid, out var transit))
        {
            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (transit.PrimaryGrid is { } primary &&
                !TerminatingOrDeleted(primary) &&
                Transform(primary).MapUid == uid)
            {
                continue;
            }

            QueueDel(uid);
            QueueAllViewerUpdates();
        }
    }

    /// <summary>
    /// Whether a ground layer overhead blocks the convoy's top layer from rising through it:
    /// true if any of that layer's grids has solid ground tiles directly above its footprint.
    /// A footprint clear of ground (a hole punched through it) lets the ship pass.
    /// </summary>
    private bool ConvoyBlockedByCeiling(EntityUid topTransitMap, EntityUid groundMap)
    {
        foreach (var grid in CollectGridsOnMap(topTransitMap))
        {
            if (TryComp<MapGridComponent>(grid, out var gridComp)
                && HasGroundUnderFootprint((grid, gridComp), groundMap))
            {
                return true;
            }
        }

        return false;
    }

    private bool LandTransitSet(Entity<MapGridComponent> grid)
    {
        if (Transform(grid).MapUid is { } transitMap && HasComp<CEZTransitMapComponent>(transitMap))
        {
            foreach (var convoyMap in GetConvoyMaps(transitMap))
            {
                foreach (var gridUid in CollectGridsOnMap(convoyMap))
                {
                    if (ZPhysicsQuery.TryComp(gridUid, out var zPhys))
                        SetZPosition((gridUid, zPhys), 0f);
                }
            }
        }

        return TryExitTransit(grid);
    }

    private void RaiseExpandEvent(EntityUid edgeMap, bool up)
    {
        if (!TryGetMapNetwork(edgeMap, out var network))
            return;

        var ev = new CEZNetworkExpandRequestEvent(network, edgeMap, up);
        RaiseLocalEvent(network.Owner, ref ev);
    }

    /// <summary>
    /// Hops every convoy layer one gap up or down, sweeping each hull through the
    /// level plane it crosses (FTL-style flattening) and relinking the new transit
    /// stack. All destination gaps are validated before anything moves.
    /// <paramref name="trackedMap"/> is rewritten to the new transit map of the layer
    /// it pointed at.
    /// </summary>
    private bool TryHopConvoyGap(List<EntityUid> convoy, ref EntityUid trackedMap, bool up)
    {
        var plans = new List<(EntityUid OldMap, EntityUid Lower, EntityUid Upper, int Offset, int Depth)>();

        foreach (var oldMap in convoy)
        {
            var old = Comp<CEZTransitMapComponent>(oldMap);

            if (up)
            {
                if (old.UpperMap is not { } newLower || !TryMapUp(newLower, out var above))
                    return false;

                plans.Add((oldMap, newLower, above.Owner, 1, above.Comp.Depth));
            }
            else
            {
                if (old.LowerMap is not { } newUpper || !TryMapDown(newUpper, out var below))
                    return false;

                plans.Add((oldMap, below.Owner, newUpper, -1, below.Comp.Depth));
            }
        }

        var tracked = trackedMap;
        EntityUid? prevNew = null;

        foreach (var (oldMap, lower, upper, offset, depth) in plans)
        {
            var old = Comp<CEZTransitMapComponent>(oldMap);
            var newTransitMap = CreateTransitMap(lower, upper, old.PrimaryGrid ?? EntityUid.Invalid);

            var newTransit = Comp<CEZTransitMapComponent>(newTransitMap);
            newTransit.ConvoyLead = old.ConvoyLead;

            if (prevNew is { } belowNew)
            {
                newTransit.TransitBelow = belowNew;
                var belowComp = Comp<CEZTransitMapComponent>(belowNew);
                belowComp.TransitAbove = newTransitMap;
                Dirty(belowNew, belowComp);
            }

            Dirty(newTransitMap, newTransit);

            var movedGrids = CollectGridsOnMap(oldMap);
            MoveGridSetToMap(movedGrids, newTransitMap, offset, depth);

            // The hop sweeps the hull through a level's plane: going up that's the new
            // gap's floor, going down its ceiling. Whatever occupies the footprint there
            // gets flattened, FTL-style.
            var crossedPlane = up ? lower : upper;
            foreach (var gridUid in movedGrids)
            {
                _shuttle.Smimsh(gridUid, crushMap: crossedPlane, explodeGrids: true);
            }

            if (oldMap == trackedMap)
                tracked = newTransitMap;

            // Unlink before deletion so shutdown promotion doesn't fire on the old stack.
            old.TransitAbove = null;
            old.TransitBelow = null;
            old.ConvoyLead = false;

            QueueDel(oldMap);
            prevNew = newTransitMap;
        }

        QueueAllViewerUpdates();
        trackedMap = tracked;
        return true;
    }

    /// <summary>
    /// Lands a transiting grid set on whichever bordering z-level is nearest to its
    /// current progress and deletes the transit map. Ships arriving at a ground
    /// layer are parked.
    /// </summary>
    public bool TryExitTransit(Entity<MapGridComponent> grid)
    {
        var xform = Transform(grid);

        if (xform.MapUid is not { } transitMap)
            return false;

        if (!TryComp<CEZTransitMapComponent>(transitMap, out var transit))
            return false;

        // Nearest plane wins: exit is just "stop being airborne".
        var up = ZPhysicsQuery.TryComp(grid, out var gridZPhys) && gridZPhys.LocalPosition >= 0.5f;

        // Validate every layer's arrival level first — the convoy lands all-or-nothing.
        var convoy = GetConvoyMaps(transitMap);
        var plans = new List<(EntityUid ConvoyMap, EntityUid Destination, int Depth)>();
        foreach (var convoyMap in convoy)
        {
            var layerTransit = Comp<CEZTransitMapComponent>(convoyMap);
            var arrivalMap = up ? layerTransit.UpperMap : layerTransit.LowerMap;
            if (arrivalMap is not { } destination ||
                !TryComp<CEZMapComponent>(destination, out var destZMap))
            {
                return false;
            }

            plans.Add((convoyMap, destination, destZMap.Depth));
        }

        foreach (var (convoyMap, destination, depth) in plans)
        {
            var movedGrids = CollectGridsOnMap(convoyMap);
            MoveGridSetToMap(movedGrids, destination, up ? 1 : -1, depth);

            var parked = HasComp<CEZGroundLayerComponent>(destination);
            foreach (var gridUid in movedGrids)
            {
                if (ZPhysicsQuery.TryComp(gridUid, out var zPhys))
                {
                    SetZPosition((gridUid, zPhys), 0f);
                    SetZVelocity((gridUid, zPhys), 0f);
                }

                _pvsOverride.RemoveGlobalOverride(gridUid);

                // Landing flattens whatever is under the hull, exactly like an FTL arrival —
                // except grids underneath blow apart in place instead of vanishing. The
                // rest of the landing set is exempt or docked ships would blast each other.
                _shuttle.Smimsh(gridUid, explodeGrids: true, ignoredGrids: movedGrids);

                if (parked)
                {
                    _shuttle.Disable(gridUid);
                    _console.RefreshShuttleConsoles(gridUid);
                }
            }

            // The set has left; a transit map only ever hosts one set.
            QueueDel(convoyMap);
        }

        QueueAllViewerUpdates();
        return true;
    }

    private EntityUid CreateTransitMap(EntityUid lowerMap, EntityUid upperMap, EntityUid primaryGrid)
    {
        var mapUid = _map.CreateMap(out _);

        var transit = AddComp<CEZTransitMapComponent>(mapUid);
        transit.LowerMap = lowerMap;
        transit.UpperMap = upperMap;
        transit.PrimaryGrid = primaryGrid;
        Dirty(mapUid, transit);

        // Same environment as the network's z-levels (atmosphere etc.), so crews don't
        // asphyxiate mid-descent.
        if (TryGetMapNetwork(lowerMap, out var network) && network.Comp.Components.Count > 0)
            EntityManager.AddComponents(mapUid, network.Comp.Components, removeExisting: false);

        // Start lit like the upper level; the client lerps this toward the lower
        // level's ambient as the grid descends.
        var light = EnsureComp<MapLightComponent>(mapUid);
        if (TryComp<MapLightComponent>(upperMap, out var upperLight))
            light.AmbientLightColor = upperLight.AmbientLightColor;
        else if (TryComp<MapLightComponent>(lowerMap, out var lowerLight))
            light.AmbientLightColor = lowerLight.AmbientLightColor;
        Dirty(mapUid, light);

        _meta.SetEntityName(mapUid, $"Z-Transit above {MetaData(lowerMap).EntityName}");

        // Viewers near the gap need eyes on the new map (PVS + decal streaming).
        QueueAllViewerUpdates();

        return mapUid;
    }
}

/// <summary>
/// Raised on a z-network entity when a transiting grid reaches the network's edge with
/// nothing beyond it (and, going down, no ground layer to land on). On-demand layer
/// generation can extend the network during this event and the grid will continue
/// into the new level seamlessly instead of stopping.
/// </summary>
[ByRefEvent]
public record struct CEZNetworkExpandRequestEvent(
    Entity<CEZMapNetworkComponent> Network,
    EntityUid EdgeMap,
    bool Up);
