/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server._Crescent.ShipShields;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Gravity;
using Content.Shared._CE.Camera;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._Mono.Ships; // Mono
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

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

    // Cache pooled support for each docked set for the current sweep.
    private readonly Dictionary<EntityUid, bool> _rigidSupportCache = new();
    private readonly TimeSpan _gravityCheckTimer = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextGravityCheckTime;

    private void UpdateGridGravity(float frameTime)
    {
        CollectPilotVerticalInputs();
        UpdateTakeoffSpool();

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

    private static float ApproachTerminal(float velocity, float signedAccel, float terminalSpeed, float frameTime)
    {
        if (terminalSpeed <= 0f || signedAccel == 0f)
            return velocity;

        var speedInDir = signedAccel > 0f ? MathF.Max(0f, velocity) : MathF.Max(0f, -velocity);
        var taper = Math.Clamp(1f - speedInDir / terminalSpeed, 0f, 1f);
        return velocity + signedAccel * taper * frameTime;
    }

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
                        if (!TryExitTransit(grid, up: false))
                            faller.Velocity = 0f;
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
                        if (!TryExitTransit(grid, up: true))
                            faller.Velocity = 0f;
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

        SetTransitAltitude(grid, altitude);
    }

    private void HandleGridTouchdown(EntityUid map, HashSet<EntityUid> grids, float impact, float crashSpeed)
    {
        if (impact <= 0f)
            return;

        var hasGround = false;
        var damagingImpact = false;
        SoundSpecifier? landingSound = null;
        var soundGrid = EntityUid.Invalid;
        var filter = Filter.Empty();

        foreach (var uid in grids)
        {
            filter.AddInGrid(uid, EntityManager);
            if (!TryComp<MapGridComponent>(uid, out var grid) ||
                !HasGroundUnderFootprint((uid, grid), map, out var damages, out var sound, checkImpact: true))
                continue;

            hasGround = true;
            damagingImpact |= damages;
            filter.AddPlayersByPvs(new EntityCoordinates(uid, grid.LocalAABB.Center), entityMan: EntityManager);
            if (landingSound == null && sound != null)
            {
                landingSound = sound;
                soundGrid = uid;
            }
        }

        if (!hasGround)
            return;

        var absorbed = false;
        if (impact >= crashSpeed)
        {
            if (landingSound != null)
                _landingAudio.PlayPvs(landingSound, soundGrid);

            if (damagingImpact)
            {
                foreach (var uid in grids)
                {
                    if (TryComp<MapGridComponent>(uid, out var grid) && TryComp<CEZGridFallerComponent>(uid, out var faller))
                        absorbed |= CrashGrid((uid, grid, faller));
                }
            }
        }

        var amplitude = MathF.Min(impact * 0.8f, 0.8f);
        RaiseNetworkEvent(new RadialShakeEvent { Amplitude = amplitude * (absorbed ? 1.5f : 1f) }, filter);
    }

    private bool CrashGrid(Entity<MapGridComponent, CEZGridFallerComponent> ent)
    {
        var tileCount = 0;
        var counter = _map.GetAllTilesEnumerator(ent, ent.Comp1);
        while (counter.MoveNext(out _))
            tileCount++;

        if (tileCount == 0)
            return false;

        var totalIntensity = tileCount * (ent.Comp2.CrashTileIntensity + ent.Comp2.CrashIntensityPerTile);
        if (_shipShields.TryAbsorbCrash(ent, totalIntensity))
            return true;

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
        return false;
    }

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

    // Non-empty terrain supports hulls, even if transparent.
    private bool HasGroundUnderFootprint(Entity<MapGridComponent> grid, EntityUid mapUid)
        => HasGroundUnderFootprint(grid, mapUid, out _, out _);

    private readonly Vector2[] _groundFootprint = new Vector2[PhysicsConstants.MaxPolygonVertices];
    private readonly Vector2[] _shipGroundFootprint = new Vector2[PhysicsConstants.MaxPolygonVertices];

    private bool HasGroundUnderFootprint(
        Entity<MapGridComponent> grid, EntityUid mapUid, out bool damagingImpact, out SoundSpecifier? landingSound,
        bool checkImpact = false)
    {
        damagingImpact = false;
        landingSound = null;
        if (!TryComp<MapGridComponent>(mapUid, out var mapGrid))
            return false;

        var hasGround = false;
        var shipMatrix = _transform.GetWorldMatrix(grid);
        var groundMatrix = _transform.GetWorldMatrix(mapUid);
        var worldAabb = shipMatrix.TransformBox(grid.Comp.LocalAABB);
        foreach (var tileRef in _map.GetTilesIntersecting(mapUid, mapGrid, worldAabb))
        {
            var groundCount = WriteTileFootprint(tileRef, mapGrid.TileSize, groundMatrix, _groundFootprint);
            var ground = _groundFootprint.AsSpan(0, groundCount);
            var min = ground[0];
            var max = min;
            foreach (var vertex in ground[1..])
            {
                min = Vector2.Min(min, vertex);
                max = Vector2.Max(max, vertex);
            }

            var overlaps = false;
            foreach (var shipTile in _map.GetTilesIntersecting(grid.Owner, grid.Comp, new Box2(min, max)))
            {
                var hullCount = WriteTileFootprint(shipTile, grid.Comp.TileSize, shipMatrix, _shipGroundFootprint);
                var hull = _shipGroundFootprint.AsSpan(0, hullCount);
                if (!TryGetWallContact(hull, 0f, ground, 0f, out var contact) || contact.Depth <= 0.001f)
                    continue;

                overlaps = true;
                break;
            }

            if (!overlaps)
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
