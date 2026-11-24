/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.ZLevels.Core.Components;
using Content.Server._Crescent.ShipShields;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Gravity;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._Mono.Ships; // Mono
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedAudioSystem _landingAudio = default!;
    [Dependency] private GravitySystem _grav = default!;
    [Dependency] private ShipShieldsSystem _shipShields = default!;
    [Dependency] private SharedShipShieldShellSystem _shieldContainment = default!; // Mono

    [Dependency] private EntityQuery<CEZMapComponent> _zMapQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physQuery = default!;

    private readonly List<Entity<MapGridComponent>> _gravityQueue = new();

    /// <summary>
    /// pzn: pooled MaxHandledMass of active gravgens per grid.
    /// </summary>
    private readonly Dictionary<EntityUid, float> _gravgenCapacity = new();

    /// <summary>
    /// pzn: per-sweep memo of each grid's rigid-set support verdict. Every grid in one rigid
    /// body shares the same verdict, so the first grid of a body computes the flood + pooled
    /// support once and caches it for every member — the other members then skip the whole
    /// recomputation. Keeps the fall gate O(grids) instead of O(grids × body size). Cleared
    /// each sweep alongside <see cref="_gravgenCapacity"/>.
    /// </summary>
    private readonly Dictionary<EntityUid, bool> _rigidSupportCache = new();
    private readonly TimeSpan _gravityCheckTimer = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextGravityCheckTime;

    /// <summary>
    /// Drop grid if no gravgen.
    /// </summary>
    private void UpdateGridGravity(float frameTime)
    {
        CollectPilotVerticalInputs();
        UpdateTakeoffSpool();

        // Throttle checking for grid gravity so the server doesn't set itself on fire.
        if (_timing.CurTime >= _nextGravityCheckTime)
        {
            _nextGravityCheckTime = _timing.CurTime + _gravityCheckTimer;

            _gravityQueue.Clear();

            _gravgenCapacity.Clear();
            _rigidSupportCache.Clear();
            var gravgenQuery = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
            while (gravgenQuery.MoveNext(out _, out var gravgen, out var gravgenXform))
            {
                if (!gravgen.GravityActive || !gravgenXform.ParentUid.IsValid())
                    continue;

                if (TryComp<CEZGravgenStunnedComponent>(gravgenXform.ParentUid, out var stunned))
                {
                    if (_timing.CurTime < stunned.End)
                        continue;

                    RemCompDeferred<CEZGravgenStunnedComponent>(gravgenXform.ParentUid);
                }

                // Unrated (<= 0) = unlimited; infinity absorbs any finite additions.
                var rated = gravgen.MaxHandledMass <= 0f ? float.PositiveInfinity : gravgen.MaxHandledMass;
                _gravgenCapacity[gravgenXform.ParentUid] =
                    _gravgenCapacity.GetValueOrDefault(gravgenXform.ParentUid) + rated;
            }

            var levelQuery = EntityQueryEnumerator<CEZGridFallerComponent, MapGridComponent>();
            while (levelQuery.MoveNext(out var uid, out var faller, out var grid))
            {
                if (_timing.CurTime < faller.GravityTime)
                    continue;

                var xform = Transform(uid);

                if (xform.MapUid is not { } mapUid || !_zMapQuery.HasComp(mapUid))
                    continue;

                if (_physQuery.TryComp(uid, out var body) && body.BodyType == BodyType.Static)
                    continue;

                // "Why not use IsWeightless on each grid-" Doesn't work on grids. I tried.
                // This also covers "you can't fall out of the ground floor": a grid sat on the
                // bottom level's terrain has ground under its footprint like any other.
                if (RigidSetHasSupport(uid))
                    continue;

                _gravityQueue.Add((uid, grid));
            }

            foreach (var grid in _gravityQueue)
            {
                if (TryComp<CEZGridFallerComponent>(grid, out var faller))
                    faller.Velocity = 0f;

                TryEnterTransit(grid); // Plummet.
            }
        }

        _gravityQueue.Clear();

        var transitQuery = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (transitQuery.MoveNext(out var transitUid, out var transit))
        {
            if (TerminatingOrDeleted(transitUid) || EntityManager.IsQueuedForDeletion(transitUid))
                continue;

            if (transit.PrimaryGrid is not { } primary ||
                TerminatingOrDeleted(primary) ||
                !TryComp<MapGridComponent>(primary, out var primaryGrid))
            {
                continue;
            }

            _gravityQueue.Add((primary, primaryGrid));
        }

        foreach (var grid in _gravityQueue)
        {
            IntegrateFallingGrid(grid, frameTime);
        }

        CheckTransitCollisions();
    }

    private readonly List<(EntityUid Map, CEZTransitMapComponent Transit, EntityUid Primary, float Progress)> _transitCollisionScan = new();
    private readonly Dictionary<EntityUid, float> _transitLastProgress = new();

    private void CheckTransitCollisions()
    {
        _transitCollisionScan.Clear();

        var query = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (query.MoveNext(out var uid, out var transit))
        {
            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (transit.LowerMap == null || transit.PrimaryGrid is not { } primary ||
                TerminatingOrDeleted(primary) ||
                !ZPhysicsQuery.TryComp(primary, out var zPhys))
            {
                continue;
            }

            _transitCollisionScan.Add((uid, transit, primary, zPhys.LocalPosition));
        }

        for (var i = 0; i < _transitCollisionScan.Count; i++)
        {
            var a = _transitCollisionScan[i];

            for (var j = i + 1; j < _transitCollisionScan.Count; j++)
            {
                var b = _transitCollisionScan[j];

                // Only sets sharing the same gap can meet.
                if (a.Transit.LowerMap != b.Transit.LowerMap || a.Transit.UpperMap != b.Transit.UpperMap)
                    continue;

                // If the maps didn't swap positions, they couldn't have collided (therefore, it is Not Our Problem)
                if (!_transitLastProgress.TryGetValue(a.Map, out var prevA) ||
                    !_transitLastProgress.TryGetValue(b.Map, out var prevB))
                {
                    continue; // first tick tracked for this pair
                }

                var prevDelta = prevA - prevB;
                var curDelta = a.Progress - b.Progress;

                // Clusters split from one gap start at the same height. Separating isn't a collision.
                if (prevDelta == 0f || prevDelta * curDelta > 0f)
                    continue;

                if (!TryGetGridSetAabb(a.Primary, out var setA, out var aabbA) ||
                    !TryGetGridSetAabb(b.Primary, out var setB, out var aabbB) ||
                    !aabbA.Intersects(aabbB))
                {
                    continue;
                }

                foreach (var gridUid in setA)
                    _shuttle.Smimsh(gridUid, crushMap: b.Map, explodeGrids: true, ignoredGrids: setA);

                foreach (var gridUid in setB)
                {
                    if (TerminatingOrDeleted(gridUid) || EntityManager.IsQueuedForDeletion(gridUid))
                        continue;

                    _shuttle.Smimsh(gridUid, crushMap: a.Map, explodeGrids: true, ignoredGrids: setB);
                }
            }
        }

        _transitLastProgress.Clear();
        foreach (var entry in _transitCollisionScan)
        {
            _transitLastProgress[entry.Map] = entry.Progress;
        }
    }

    /// <summary>
    /// Collects a transit set and the union of its members' world AABBs.
    /// </summary>
    private bool TryGetGridSetAabb(EntityUid primary, out HashSet<EntityUid> set, out Box2 aabb)
    {
        set = CollectTransitSet(primary);
        aabb = default;

        var first = true;
        foreach (var gridUid in set)
        {
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var worldAabb = _transform.GetWorldMatrix(gridUid).TransformBox(grid.LocalAABB);
            aabb = first ? worldAabb : aabb.Union(worldAabb);
            first = false;
        }

        return !first;
    }

    /// <summary>
    /// Someone forgot their gravgen.
    /// </summary>
    private static float ApproachTerminal(float velocity, float signedAccel, float terminalSpeed, float frameTime)
    {
        if (terminalSpeed <= 0f || signedAccel == 0f)
            return velocity;

        var speedInDir = signedAccel > 0f ? MathF.Max(0f, velocity) : MathF.Max(0f, -velocity);
        var taper = Math.Clamp(1f - speedInDir / terminalSpeed, 0f, 1f);
        return velocity + signedAccel * taper * frameTime;
    }

    /// <summary>
    /// Moves a value toward a target by at most <paramref name="maxDelta"/>.
    /// </summary>
    private static float MoveTowards(float current, float target, float maxDelta)
    {
        var diff = target - current;
        return MathF.Abs(diff) <= maxDelta ? target : current + MathF.Sign(diff) * maxDelta;
    }

    private void IntegrateFallingGrid(Entity<MapGridComponent> grid, float frameTime)
    {
        if (!TryComp<CEZGridFallerComponent>(grid, out var faller) ||
            !ZPhysicsQuery.TryComp(grid, out var zPhys))
        {
            return;
        }

        if (Transform(grid).MapUid is not { } transitMap ||
            !TryComp<CEZTransitMapComponent>(transitMap, out var transit) ||
            transit.LowerMap is not { } lowerMap ||
            !TryComp<CEZMapComponent>(lowerMap, out var lowerZ))
        {
            return;
        }

        var progress = zPhys.LocalPosition;
        var span = GetTransitSpan(transit);
        var distanceBelow = progress * span;
        var distanceAbove = span - distanceBelow;

        var transitSet = CollectGridsOnMap(transitMap);
        var input = GetTransitVerticalInput(transitMap);

        TryComp<PlanetTransitComponent>(grid, out var planetTransit);
        if (planetTransit != null &&
            planetTransit.Direction == PlanetTransitDirection.Ascent &&
            planetTransit.Phase == PlanetTransitPhase.Charging)
        {
            faller.Velocity = 0f;
            foreach (var member in transitSet)
                SetZVelocity(member, 0f);
            return;
        }

        // The whole rigid set hovers only if its generators' pooled capacity holds the
        // set's pooled mass — not if any single member's generator holds only itself.
        var hasGravgen = HasPooledGravgenSupport(transitSet);

        if (!hasGravgen)
        {
            // AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
            if (_timing.CurTime < faller.GravityTime)
                return;

            faller.Velocity = ApproachTerminal(faller.Velocity, faller.GridGravity, faller.GridTerminalVelocity, frameTime);
        }
        else
        {
            var accel = GetVerticalThrustAccel(grid);
            var damp = Math.Max(accel, HoverDampAccel);

            if (input != 0f && accel > 0f)
            {
                // Flight.
                faller.Velocity = ApproachTerminal(faller.Velocity, -input * accel, MaxPilotVerticalSpeed, frameTime);
            }
            else if ((transit.UpperMap == null || transit.AutoLand) &&
                     (distanceBelow > SettleZone ||
                      transit.AutoLand && !TransitBlockedByPlane(transitMap, lowerMap)))
            {
                var target = Math.Clamp(distanceBelow * ApproachGain, TouchdownSpeed, MaxPilotVerticalSpeed);
                faller.Velocity = MoveTowards(faller.Velocity, target, damp * frameTime);
            }
            else
            {
                var target = 0f;

                if (distanceBelow <= SettleZone)
                {
                    if (distanceBelow <= TouchdownProgress && MathF.Abs(faller.Velocity) <= ExitTransitMaxSpeed)
                    {
                        faller.Velocity = 0f;
                        TryExitTransit(grid, up: false);
                        return;
                    }

                    target = MathF.Max(TouchdownSpeed, distanceBelow * ApproachGain);
                }
                // A solid ceiling stops ascent rather than letting a ship settle on top of it.
                else if (!transit.AutoLand && distanceAbove <= SettleZone
                         && transit.UpperMap is { } upper
                         && !TransitBlockedByPlane(transitMap, upper))
                {
                    if (distanceAbove <= TouchdownProgress && MathF.Abs(faller.Velocity) <= ExitTransitMaxSpeed)
                    {
                        faller.Velocity = 0f;
                        TryExitTransit(grid, up: true);
                        return;
                    }

                    target = -MathF.Max(TouchdownSpeed, distanceAbove * ApproachGain);
                }

                faller.Velocity = MoveTowards(faller.Velocity, target, damp * frameTime);
            }

            // Slow down when approaching terrain so people under you got some time to move.
            if (faller.Velocity > 0f && TransitBlockedByPlane(transitMap, lowerMap))
            {
                var cap = MathF.Max(TouchdownSpeed, distanceBelow * ApproachGain);
                if (faller.Velocity > cap)
                    faller.Velocity = MoveTowards(faller.Velocity, cap, damp * frameTime);
            }
        }

        foreach (var member in transitSet)
        {
            SetZVelocity(member, -faller.Velocity);

            if (member != grid.Owner && TryComp<CEZGridFallerComponent>(member, out var memberFaller))
                memberFaller.Velocity = faller.Velocity;
        }

        var currentAltitude = lowerZ.Depth + distanceBelow;
        var altitude = currentAltitude - faller.Velocity * frameTime;
        // Clamp the docked set at the first shield contact.
        var delta = altitude - currentAltitude;
        var pushing = input != 0f;
        foreach (var member in transitSet)
        {
            var from = GetAbsoluteAltitude(member);
            var to = from + delta;
            var clamped = _shieldContainment.ClampVertical(member, Transform(member).MapUid, from, to,
                pushing, gridFootprint: true, transitSet);
            if (clamped != to)
                delta = clamped - from;
        }

        if (delta != altitude - currentAltitude)
        {
            altitude = currentAltitude + delta;
            faller.Velocity = 0f;
            foreach (var member in transitSet)
                SetZVelocity(member, 0f);
        }
        // Mono End

        if (!SetTransitAltitude(grid, altitude))
            return;

        // Still airborne?
        if (HasComp<CEZTransitMapComponent>(Transform(grid).MapUid))
            return;

        var impact = faller.Velocity;
        faller.Velocity = 0f;

        // Only a landing that ended ON terrain is a crash; setting down over open sky isn't.
        if (impact < faller.GridCrashVelocity
            || Transform(grid).MapUid is not { } landedMap
            || !HasGroundUnderFootprint(grid, landedMap, out var damagingImpact, out var landingSound, checkImpact: true))
        {
            return;
        }

        if (landingSound != null)
            _landingAudio.PlayPvs(landingSound, grid);
        if (!damagingImpact)
            return;

        foreach (var landedUid in transitSet)
        {
            if (TryComp<MapGridComponent>(landedUid, out var landedGrid) && TryComp<CEZGridFallerComponent>(landedUid, out var landedFaller))
                CrashGrid((landedUid, landedGrid, landedFaller));
        }
    }

    /// <summary>
    /// kaboom?
    /// </summary>
    private void CrashGrid(Entity<MapGridComponent, CEZGridFallerComponent> ent)
    {
        var tileCount = 0;
        var counter = _map.GetAllTilesEnumerator(ent, ent.Comp1);
        while (counter.MoveNext(out _))
            tileCount++;

        if (tileCount == 0)
            return;

        var totalIntensity = tileCount * (ent.Comp2.CrashTileIntensity + ent.Comp2.CrashIntensityPerTile);
        if (_shipShields.TryAbsorbCrash(ent, totalIntensity))
            return;

        var tiles = _map.GetAllTilesEnumerator(ent, ent.Comp1);
        while (tiles.MoveNext(out var tileRef))
        {
            var coords = _map.GridTileToLocal(ent, ent.Comp1, tileRef.Value.GridIndices);
            _explosion.QueueExplosion(coords,
                ExplosionSystem.DefaultExplosionPrototypeId,
                ent.Comp2.CrashTileIntensity,
                ent.Comp2.CrashTileSlope,
                ent.Comp2.CrashTileMaxIntensity,
                cause: ent,
                addLog: false);
        }

        _explosion.QueueExplosion(ent.Owner,
            ExplosionSystem.DefaultExplosionPrototypeId,
            ent.Comp2.CrashIntensityPerTile * tileCount,
            ent.Comp2.CrashCenterSlope,
            ent.Comp2.CrashCenterMaxIntensity);
    }

    /// <summary>
    /// Will It Lift?
    /// </summary>
    private bool HasPooledGravgenSupport(IEnumerable<EntityUid> grids)
    {
        var capacity = 0f;
        var mass = 0f;

        foreach (var grid in grids)
        {
            if (_gravgenCapacity.TryGetValue(grid, out var gridCapacity))
            {
                if (float.IsPositiveInfinity(gridCapacity))
                    return true;

                capacity += gridCapacity;
            }

            if (_physQuery.TryComp(grid, out var body))
                mass += body.FixturesMass;
        }

        // capacity > 0 means at least one active generator exists; a set with no lift
        // hardware is never "supported" even when it happens to be massless.
        return capacity > 0f && mass <= capacity;
    }

    /// <summary>Checks lift and ground support once per same-map docked set per sweep.</summary>
    private bool RigidSetHasSupport(EntityUid uid)
    {
        if (_rigidSupportCache.TryGetValue(uid, out var cached))
            return cached;

        var set = CollectGridSet(uid);
        var supported = SetHasSupport(set);

        foreach (var member in set)
            _rigidSupportCache[member] = supported;

        return supported;
    }

    /// <summary>Pooled gravgen lift over the set covers its pooled mass, or a member is on ground.</summary>
    private bool SetHasSupport(HashSet<EntityUid> set)
    {
        if (HasPooledGravgenSupport(set))
            return true;

        foreach (var member in set)
        {
            if (Transform(member).MapUid is { } memberMap
                && TryComp<MapGridComponent>(member, out var memberGrid)
                && HasGroundUnderFootprint((member, memberGrid), memberMap))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// pzn: Get the current load of the grid for gravgen examine.
    /// </summary>
    public bool TryGetGravgenLoad(EntityUid gridUid, out float gridMass, out float capacity)
    {
        gridMass = 0f;
        capacity = 0f;

        if (!HasComp<MapGridComponent>(gridUid))
            return false;

        var grids = CollectTransitSet(gridUid);

        var query = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var gravgen, out var xform))
        {
            if (!gravgen.GravityActive)
                continue;

            if (!grids.Contains(xform.ParentUid))
                continue;

            capacity += gravgen.MaxHandledMass < 0f ? float.PositiveInfinity : gravgen.MaxHandledMass;
        }

        foreach (var member in grids)
        {
            if (_physQuery.TryComp(member, out var body))
                gridMass += body.FixturesMass;
        }

        return true;
    }

    /// <summary>
    /// Whether a z-level has solid terrain under (or over) a grid's footprint. A z-level map
    /// entity carries its own MapGrid and its tiles are the layer's terrain, so this is what
    /// makes a level something a ship can rest on or be stopped by.
    ///
    /// Solid is a non-empty tile — the same rule the entity fall path
    /// (<c>ComputeGroundHeightInternal</c>) uses, so a gap punched in a platform drops a ship
    /// through exactly like it drops a person instead of the two disagreeing about the same tile.
    ///
    /// Emptiness rather than <see cref="ContentTileDefinition.Transparent"/>, deliberately:
    /// transparency is a SIGHT property, driving roof occlusion and the look-up/look-down view, and
    /// a floor you can see through is still a floor. Lattice, glass floor and damaged plating are
    /// all transparent and all hold weight, so keying support off it dropped ships through tiles
    /// people were standing on. Space is tile id 0, so it is empty here anyway.
    /// </summary>
    private bool HasGroundUnderFootprint(Entity<MapGridComponent> grid, EntityUid mapUid)
        => HasGroundUnderFootprint(grid, mapUid, out _, out _);

    private bool HasGroundUnderFootprint(
        Entity<MapGridComponent> grid, EntityUid mapUid, out bool damagingImpact, out SoundSpecifier? landingSound,
        bool checkImpact = false)
    {
        damagingImpact = false;
        landingSound = null;
        if (!TryComp<MapGridComponent>(mapUid, out var mapGrid))
            return false;

        var hasGround = false;
        var worldAabb = _transform.GetWorldMatrix(grid).TransformBox(grid.Comp.LocalAABB);
        var tiles = _map.GetTilesEnumerator(mapUid, mapGrid, worldAabb);
        while (tiles.MoveNext(out var tileRef))
        {
            if (tileRef.Tile.IsEmpty)
                continue;

            if (!checkImpact)
                return true;

            hasGround = true;
            var tile = TilDefMan[tileRef.Tile.TypeId] as ContentTileDefinition;
            damagingImpact |= tile?.FallDamageMultiplier != 0f;
            landingSound ??= tile?.LandingSound;
            if (damagingImpact && landingSound != null)
                break;
        }

        return hasGround;
    }
}
