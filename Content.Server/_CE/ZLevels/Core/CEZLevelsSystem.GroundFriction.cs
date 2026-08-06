/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Movement.Events;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>
/// Ground contact for grids flying at a z-level, rather than through the gap between two.
///
/// The engine refuses to give a map-grid physics at all (SharedPhysicsSystem.OnGridAdd bails on
/// anything with a MapComponent), so a level's terrain is invisible to the solver and ships fly
/// straight through it. Landing therefore only ever happened on the transit path, where
/// <see cref="TryExitTransit"/> parks a grid that arrives over solid ground.
///
/// A grid sitting on a z-level is at <see cref="CEZPhysicsComponent.LocalPosition"/> zero — flush
/// with that level's floor — so sharing a level with terrain IS contact with it. Such a grid gets
/// dragged down and parked once it has stopped, which gives the missing case: skidding to a halt on
/// the ground you flew in over.
///
/// The skid is two terms, because neither alone reads right. A speed-proportional one (the engine's
/// own damping, via <see cref="TileFrictionEvent"/>) gives the hard initial bite when you come in
/// fast, but decays exponentially and so asymptotes rather than stopping. A constant deceleration
/// (Coulomb, the real model for a sliding contact) carries the tail: it ramps velocity down linearly
/// and reaches zero in finite time, so the hull actually grinds out instead of creeping forever.
/// </summary>
public sealed partial class CEZLevelsSystem
{
    /// <summary>
    /// Speed-proportional part of the scrape, as a multiplier on the grid's ordinary airborne
    /// damping. Looks large only because that baseline is deliberately tiny —
    /// <c>physics.air_friction</c> (0.2) times ShuttleComponent.BodyModifier (0.25) is 0.05 — so at
    /// full footprint coverage this lands near 1.0 damping: a ~1 second e-fold, biting hardest at
    /// the moment of contact and fading as the hull slows.
    /// </summary>
    private const float GroundDragModifier = 20f;

    /// <summary>
    /// Constant part of the scrape (m/s²) at full footprint coverage. Sets the skid's character:
    /// against both terms together a hull touching down at 8 m/s comes to rest in about 1.3 seconds
    /// and 4 tiles, most of that speed shed in the first half-second while the proportional term is
    /// still biting and the rest ground out at a steady 3 m/s².
    ///
    /// Also the thrust the contact can beat — a ship whose lateral acceleration exceeds this can
    /// still drag itself along the ground, which is the honest behaviour for something absurdly
    /// overpowered and is well above what ordinary hulls manage.
    /// </summary>
    private const float GroundSkidDecel = 3f;

    /// <summary>
    /// Constant part of the scrape applied to spin (rad/s²) at full coverage. Kills the yaw of a
    /// hull that came in sideways over roughly the same time the linear term kills its speed.
    /// </summary>
    private const float GroundSkidAngularDecel = 1.5f;

    /// <summary>
    /// Speed (m/s) under which a scraping grid is considered stopped and gets parked.
    /// </summary>
    private const float GroundingSpeed = 0.15f;

    /// <summary>
    /// Spin (rad/s) under which a scraping grid is considered stopped and gets parked.
    /// </summary>
    private const float GroundingAngularSpeed = 0.05f;

    /// <summary>
    /// Per-grid footprint coverage, memoised for the tick it was computed on. The friction
    /// controller asks once per awake body per substep and the grounding sweep asks again, so
    /// without this a large hull re-walks its whole footprint several times a tick.
    /// </summary>
    private readonly Dictionary<EntityUid, (GameTick Tick, float Coverage)> _groundCoverageCache = new();

    private void InitializeGroundFriction()
    {
        SubscribeLocalEvent<CEZGridFallerComponent, TileFrictionEvent>(OnGridTileFriction);
    }

    /// <summary>
    /// The speed-proportional half of the scrape. Scales with how much of the hull is actually over
    /// solid tiles, so clipping a platform corner barely bites while a full belly landing digs in.
    /// </summary>
    private void OnGridTileFriction(Entity<CEZGridFallerComponent> ent, ref TileFrictionEvent args)
    {
        var coverage = GetGroundCoverage(ent.Owner);
        if (coverage <= 0f)
            return;

        args.Modifier *= 1f + (GroundDragModifier - 1f) * coverage;
    }

    /// <summary>
    /// Runs the constant half of the scrape and parks whatever it has brought to a stop. Parking is
    /// per-grid rather than per-rigid-set, matching <see cref="TryExitTransit"/>: a wide set can
    /// straddle a platform edge, and the first member to park flips its network's
    /// <see cref="CEZGridNetworkComponent.HasStaticAnchor"/> anyway, which pins the rest.
    /// </summary>
    private void UpdateGroundFriction(float frameTime)
    {
        // Coverage is only ever valid for the tick it was taken on, and grids die; drop the lot
        // rather than carrying stale entries for deleted hulls.
        _groundCoverageCache.Clear();

        var query = EntityQueryEnumerator<CEZGridFallerComponent, MapGridComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var body))
        {
            // Already parked, or still in the air between two levels.
            if (body.BodyType == BodyType.Static)
                continue;

            var coverage = GetGroundCoverage(uid);
            if (coverage <= 0f)
                continue;

            ApplySkidDeceleration(uid, body, coverage, frameTime);

            if (body.LinearVelocity.LengthSquared() > GroundingSpeed * GroundingSpeed
                || MathF.Abs(body.AngularVelocity) > GroundingAngularSpeed)
            {
                continue;
            }

            _shuttle.Disable(uid);
            _console.RefreshShuttleConsoles(uid);
        }
    }

    /// <summary>
    /// Sheds a fixed amount of speed and spin per second, capped at whatever the hull has left so
    /// the scrape can never drive it backwards. Applied per-grid off its own coverage; a z-network
    /// straddling an edge then decelerates by its supported mass share once
    /// <see cref="CEZGridSyncSystem"/> equalises momentum across the members.
    /// </summary>
    private void ApplySkidDeceleration(EntityUid uid, PhysicsComponent body, float coverage, float frameTime)
    {
        var velocity = body.LinearVelocity;
        var speed = velocity.Length();

        if (speed > 0f)
        {
            var drop = MathF.Min(GroundSkidDecel * coverage * frameTime, speed);
            _physics.SetLinearVelocity(uid, velocity - velocity / speed * drop, body: body);
        }

        var spin = body.AngularVelocity;

        if (spin != 0f)
        {
            var angularDrop = MathF.Min(GroundSkidAngularDecel * coverage * frameTime, MathF.Abs(spin));
            _physics.SetAngularVelocity(uid, spin - MathF.Sign(spin) * angularDrop, body: body);
        }
    }

    /// <summary>
    /// Fraction of a grid's footprint sitting over solid terrain on the z-level it occupies, 0 if
    /// it is not on one (a transit map, or a level with no terrain grid of its own).
    ///
    /// Measured tile-for-tile against the hull's OWN tiles, not its world AABB: a turned hull's AABB
    /// is the bounding box of the rotated rectangle and juts out over terrain the ship isn't actually
    /// above, which had it grounding on thin air near the corners. Solid is a non-empty tile, the
    /// same rule entity falling and <see cref="HasGroundUnderFootprint"/> use, so nothing disagrees
    /// about whether a given tile is a hole.
    /// </summary>
    private float GetGroundCoverage(EntityUid grid)
    {
        if (_groundCoverageCache.TryGetValue(grid, out var cached) && cached.Tick == _timing.CurTick)
            return cached.Coverage;

        var coverage = ComputeGroundCoverage(grid);
        _groundCoverageCache[grid] = (_timing.CurTick, coverage);
        return coverage;
    }

    private float ComputeGroundCoverage(EntityUid grid)
    {
        if (!_mapGridQuery.TryComp(grid, out var gridComp))
            return 0f;

        // A transit map has no CEZMapComponent, so this also excludes grids mid-flight between
        // levels — those are the falling code's business, not ours.
        var mapUid = Transform(grid).MapUid;
        if (mapUid is not { } map || !_zMapQuery.HasComp(map) || !_mapGridQuery.TryComp(map, out var mapGrid))
            return 0f;

        var gridMatrix = _transform.GetWorldMatrix(grid);
        var tileSize = gridComp.TileSize;

        var solid = 0;
        var total = 0;

        // Walk the ship's real tiles and test the terrain directly beneath each tile's centre. This
        // follows the hull's actual shape and rotation instead of its bounding box.
        var shipTiles = _map.GetAllTilesEnumerator(grid, gridComp);
        while (shipTiles.MoveNext(out var shipTile))
        {
            total++;

            // Tile centre in the ship's local frame (metres), then into the world.
            var localCentre = new Vector2(
                (shipTile.Value.GridIndices.X + 0.5f) * tileSize,
                (shipTile.Value.GridIndices.Y + 0.5f) * tileSize);
            var worldPos = Vector2.Transform(localCentre, gridMatrix);

            if (_map.TryGetTileRef(map, mapGrid, worldPos, out var terrainTile)
                && !terrainTile.Tile.IsEmpty)
            {
                solid++;
            }
        }

        return total == 0 ? 0f : solid / (float)total;
    }
}
