/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Movement.Events;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>
/// Ground contact for grids flying at a z-level, rather than through the gap between two.
///
/// The engine refuses to give a map-grid physics at all (SharedPhysicsSystem.OnGridAdd bails on
/// anything with a MapComponent), so a level's terrain is invisible to the solver and ships fly
/// straight through it. Landing therefore only ever happened on the transit path.
///
/// A grid sitting on a z-level is at <see cref="CEZPhysicsComponent.LocalPosition"/> zero — flush
/// with that level's floor — so sharing a level with terrain IS contact with it. Such a grid gets
/// dragged down, which gives the missing case: skidding to a halt on the ground you flew in over.
///
/// The skid is two terms, because neither alone reads right. A speed-proportional one (the engine's
/// own damping, via <see cref="TileFrictionEvent"/>) gives the hard initial bite when you come in
/// fast, but decays exponentially and so asymptotes rather than stopping. A constant deceleration
/// (Coulomb, the real model for a sliding contact) carries the tail: it ramps velocity down linearly
/// and reaches zero in finite time, so the hull actually grinds out instead of creeping forever.
///
/// Nothing here parks a grid or otherwise changes its body type. A hull that has stopped is one the
/// scrape is holding still, and it stays an ordinary dynamic body the whole time — so "can this ship
/// move?" has exactly one answer, thrust against friction, evaluated by the solver every tick like
/// any other force. There is no landed state to enter, get stuck in, or have to be released from.
/// </summary>
public sealed partial class CEZLevelsSystem
{
    /// <summary>
    /// Speed-proportional part of the scrape, as a multiplier on the grid's ordinary airborne
    /// damping. Looks large only because that baseline is deliberately tiny —
    /// <c>physics.air_friction</c> (0.2) times ShuttleComponent.BodyModifier (0.25) is 0.05 — so
    /// this lands near 1.0 damping: a ~1 second e-fold, biting hardest at the moment of contact and
    /// fading as the hull slows.
    /// </summary>
    private const float GroundDragModifier = 20f;

    /// <summary>
    /// Constant part of the scrape (m/s²), and — being the same number — the acceleration a hull
    /// must out-pull to drag itself along the ground at all.
    ///
    /// Absolute, NOT a multiple of the hull's own thrust. Scaling it to the ship is a trap: it gives
    /// every hull an identical thrust-to-friction ratio, so "can this ship drive off the deck?"
    /// stops depending on the ship and collapses to one global yes or no. A 493kg Bucket pulling
    /// 0.81 m/s² and a dreadnought pulling a hundred times that came out exactly alike. Real sliding
    /// friction is μg — a property of the contact, not the engine — which is what makes thrust-to-
    /// mass the thing that decides, so an underpowered hull is genuinely stuck and a monster
    /// genuinely grinds along.
    ///
    /// One number for the scrape and for that threshold, because in a Coulomb contact they ARE one
    /// number: net acceleration is simply thrust minus this. It also makes the two properties move
    /// together the way a real surface does — a grippier deck both stops you sooner and is harder to
    /// drive on — instead of being tuned apart into a hull that is free but cannot move.
    ///
    /// Scaled by footprint coverage, so a hull half over a hole gets half the grip — but sized so
    /// that band is narrow rather than a place to live in. Any linear threshold has a coverage where
    /// thrust just pips friction and the ship creeps; what decides whether that is a nuisance is how
    /// wide the band is. A Bucket pulls 0.81 m/s², so it breaks free below <c>0.81/decel</c>
    /// coverage: at 6 that was everything under 13% of the hull, wide enough to sit in and inch
    /// along, and at 20 it is under 4% — a few tiles of a 987-tile hull, which is a corner clip and
    /// should let go.
    ///
    /// At full coverage this stops a touchdown at 8 m/s inside about 1.6 metres, and sits above what
    /// all but the 100x-thruster hulls can pull, so dragging yourself along the deck stays the
    /// preserve of the genuinely absurd.
    /// </summary>
    public const float GroundSkidDecel = 20f;

    /// <summary>
    /// Constant part of the scrape applied to spin (rad/s²) at full coverage. Kills the yaw of a
    /// hull that came in sideways over roughly the same time the linear term kills its speed.
    /// </summary>
    public const float GroundSkidAngularDecel = 10f;

    /// <summary>
    /// Per-grid footprint coverage, memoised for the tick it was computed on. The friction
    /// controller asks once per awake body per substep and the skid sweep asks again, so without
    /// this a large hull re-walks its whole footprint several times a tick.
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
    /// Once-a-tick upkeep: drops the coverage memo and republishes each grid's ground contact for
    /// the shuttle console. The scrape itself is <see cref="CEZGroundFrictionController"/>'s job.
    /// </summary>
    private void UpdateGroundFriction()
    {
        // Coverage is only ever valid for the tick it was taken on, and grids die; drop the lot
        // rather than carrying stale entries for deleted hulls.
        _groundCoverageCache.Clear();

        var query = EntityQueryEnumerator<CEZGridFallerComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            SetGroundContact(uid, GetGroundCoverage(uid) > 0f);
        }
    }

    /// <summary>
    /// Fraction of a grid's footprint sitting over solid terrain on the z-level it occupies, 0 if
    /// it is not on one — a transit map, or a level with no terrain grid of its own.
    /// </summary>
    public float GetGroundCoverage(EntityUid grid)
    {
        if (_groundCoverageCache.TryGetValue(grid, out var cached) && cached.Tick == _timing.CurTick)
            return cached.Coverage;

        var coverage = ComputeGroundCoverage(grid);
        _groundCoverageCache[grid] = (_timing.CurTick, coverage);
        return coverage;
    }

    /// <summary>
    /// Measured against the hull's OWN tiles, not its world AABB: a turned hull's AABB is the
    /// bounding box of the rotated rectangle and juts out over terrain the ship isn't actually
    /// above, which had it grounding on thin air near the corners.
    ///
    /// Each tile contributes a FRACTION, bilinearly interpolated from the four terrain tiles around
    /// its centre, rather than a yes/no on the one tile it happens to sit over. Point-sampling reads
    /// as if it should give fine-grained coverage — a 987-tile hull ought to move in 0.1% steps —
    /// but the samples are perfectly correlated: on an axis-aligned hull every tile centre crosses
    /// its terrain boundary at the same instant, so coverage does not creep up, it snaps from none
    /// to all as the ship slides half a tile. Subsampling within each tile does not help for the
    /// same reason. Interpolating instead makes coverage a continuous function of position, so grip
    /// ramps in over the last tile of travel and a hull edging onto solid ground is progressively
    /// caught rather than seized at one arbitrary threshold.
    ///
    /// Solid is a non-empty tile, the same rule entity falling and
    /// <see cref="HasGroundUnderFootprint"/> use, so nothing disagrees about whether a given tile is
    /// a hole.
    /// </summary>
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

        var solid = 0f;
        var total = 0;

        var shipTiles = _map.GetAllTilesEnumerator(grid, gridComp);
        while (shipTiles.MoveNext(out var shipTile))
        {
            total++;

            // Tile centre in the ship's local frame (metres), then into the world.
            var localCentre = new Vector2(
                (shipTile.Value.GridIndices.X + 0.5f) * tileSize,
                (shipTile.Value.GridIndices.Y + 0.5f) * tileSize);
            var worldPos = Vector2.Transform(localCentre, gridMatrix);

            solid += SampleSolidity(map, mapGrid, worldPos);
        }

        return total == 0 ? 0f : solid / total;
    }

    /// <summary>
    /// Solidity of the terrain under a world point, in 0..1, bilinearly interpolated between the
    /// four terrain tile centres surrounding it. Exactly over a solid tile's centre this is 1, over
    /// a hole's centre 0, and it slides smoothly across the boundary between them.
    /// </summary>
    private float SampleSolidity(EntityUid map, MapGridComponent mapGrid, Vector2 worldPos)
    {
        // Tile-space position, shifted so integer coordinates land on tile CENTRES — those are the
        // points whose solidity we actually know.
        var local = _map.WorldToLocal(map, mapGrid, worldPos) / mapGrid.TileSize;
        var sampleX = local.X - 0.5f;
        var sampleY = local.Y - 0.5f;

        var x0 = (int) MathF.Floor(sampleX);
        var y0 = (int) MathF.Floor(sampleY);
        var fracX = sampleX - x0;
        var fracY = sampleY - y0;

        var bottom = MathHelper.Lerp(
            IsSolidTile(map, mapGrid, x0, y0),
            IsSolidTile(map, mapGrid, x0 + 1, y0),
            fracX);
        var top = MathHelper.Lerp(
            IsSolidTile(map, mapGrid, x0, y0 + 1),
            IsSolidTile(map, mapGrid, x0 + 1, y0 + 1),
            fracX);

        return MathHelper.Lerp(bottom, top, fracY);
    }

    private float IsSolidTile(EntityUid map, MapGridComponent mapGrid, int x, int y)
    {
        return _map.TryGetTileRef(map, mapGrid, new Vector2i(x, y), out var tileRef) && !tileRef.Tile.IsEmpty
            ? 1f
            : 0f;
    }
}
