/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Maps;
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
    /// Per-grid footprint grip and contact, memoised for the tick they were computed on. The
    /// friction controller asks once per awake body per substep and the upkeep sweep asks again, so
    /// without this a large hull re-walks its whole footprint several times a tick.
    /// </summary>
    private readonly Dictionary<EntityUid, (GameTick Tick, float Grip, bool Contact)> _groundCoverageCache = new();

    private void InitializeGroundFriction()
    {
        SubscribeLocalEvent<CEZGridFallerComponent, TileFrictionEvent>(OnGridTileFriction);
    }

    /// <summary>
    /// The speed-proportional half of the scrape. Scales with the grip the hull is actually getting,
    /// so clipping a platform corner barely bites, a full belly landing digs in, and a hull over a
    /// frictionless surface is left alone entirely.
    /// </summary>
    private void OnGridTileFriction(Entity<CEZGridFallerComponent> ent, ref TileFrictionEvent args)
    {
        var grip = GetGroundGrip(ent.Owner);
        if (grip <= 0f)
            return;

        args.Modifier *= 1f + (GroundDragModifier - 1f) * grip;
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
            SetGroundContact(uid, HasGroundContact(uid));
        }
    }

    /// <summary>
    /// How much grip a grid's footprint is getting from the z-level it occupies: the mean of the
    /// terrain's <see cref="ContentTileDefinition.Friction"/> under each of its tiles, so ordinary
    /// ground gives 1, grippier decking like lattice gives more, a frictionless surface gives 0, and
    /// open sky gives nothing. Multiplies the scrape directly.
    /// </summary>
    public float GetGroundGrip(EntityUid grid)
    {
        return GetGroundSample(grid).Grip;
    }

    /// <summary>
    /// Whether any of a grid's footprint is over solid terrain at all. Distinct from grip: a hull
    /// sitting on a frictionless surface is still very much on the ground, it just slides, so the
    /// console readout must not be driven by how much the terrain bites.
    /// </summary>
    public bool HasGroundContact(EntityUid grid)
    {
        return GetGroundSample(grid).Contact;
    }

    private (float Grip, bool Contact) GetGroundSample(EntityUid grid)
    {
        if (_groundCoverageCache.TryGetValue(grid, out var cached) && cached.Tick == _timing.CurTick)
            return (cached.Grip, cached.Contact);

        var sample = ComputeGroundSample(grid);
        _groundCoverageCache[grid] = (_timing.CurTick, sample.Grip, sample.Contact);
        return sample;
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
    /// its terrain boundary at the same instant, so grip does not creep up, it snaps from none to
    /// all as the ship slides half a tile. Subsampling within each tile does not help for the same
    /// reason. Interpolating instead makes grip a continuous function of position, so it ramps in
    /// over the last tile of travel and a hull edging onto solid ground is progressively caught
    /// rather than seized at one arbitrary threshold. It also means a shoreline is a gradient rather
    /// than a wall: a hull coming off water is grabbed over a tile of travel, not instantly.
    /// </summary>
    private (float Grip, bool Contact) ComputeGroundSample(EntityUid grid)
    {
        if (!_mapGridQuery.TryComp(grid, out var gridComp))
            return (0f, false);

        // A transit map has no CEZMapComponent, so this also excludes grids mid-flight between
        // levels — those are the falling code's business, not ours.
        var mapUid = Transform(grid).MapUid;
        if (mapUid is not { } map || !_zMapQuery.HasComp(map) || !_mapGridQuery.TryComp(map, out var mapGrid))
            return (0f, false);

        var gridMatrix = _transform.GetWorldMatrix(grid);
        var tileSize = gridComp.TileSize;

        var grip = 0f;
        var total = 0;
        var contact = false;

        var shipTiles = _map.GetAllTilesEnumerator(grid, gridComp);
        while (shipTiles.MoveNext(out var shipTile))
        {
            total++;

            // Tile centre in the ship's local frame (metres), then into the world.
            var localCentre = new Vector2(
                (shipTile.Value.GridIndices.X + 0.5f) * tileSize,
                (shipTile.Value.GridIndices.Y + 0.5f) * tileSize);
            var worldPos = Vector2.Transform(localCentre, gridMatrix);

            grip += SampleGrip(map, mapGrid, worldPos, ref contact);
        }

        return (total == 0 ? 0f : grip / total, contact);
    }

    /// <summary>
    /// Grip of the terrain under a world point, bilinearly interpolated between the four terrain
    /// tile centres surrounding it, and flags whether any of them was solid at all.
    ///
    /// Grip is the tile's own <see cref="ContentTileDefinition.Friction"/> rather than a flat 1, so
    /// what the ground does to a hull is content data. A water tile at <c>friction: 0</c> lets a
    /// ship glide across a lake and still be caught the moment it reaches the bank, and ice, decking
    /// and dirt all fall out of the same number without any of them being special-cased here.
    /// </summary>
    private float SampleGrip(EntityUid map, MapGridComponent mapGrid, Vector2 worldPos, ref bool contact)
    {
        // Tile-space position, shifted so integer coordinates land on tile CENTRES — those are the
        // points whose grip we actually know.
        var local = _map.WorldToLocal(map, mapGrid, worldPos) / mapGrid.TileSize;
        var sampleX = local.X - 0.5f;
        var sampleY = local.Y - 0.5f;

        var x0 = (int) MathF.Floor(sampleX);
        var y0 = (int) MathF.Floor(sampleY);
        var fracX = sampleX - x0;
        var fracY = sampleY - y0;

        var bottom = MathHelper.Lerp(
            TileGrip(map, mapGrid, x0, y0, ref contact),
            TileGrip(map, mapGrid, x0 + 1, y0, ref contact),
            fracX);
        var top = MathHelper.Lerp(
            TileGrip(map, mapGrid, x0, y0 + 1, ref contact),
            TileGrip(map, mapGrid, x0 + 1, y0 + 1, ref contact),
            fracX);

        return MathHelper.Lerp(bottom, top, fracY);
    }

    /// <summary>
    /// A single terrain tile's grip: its friction, or nothing at all if it is a hole. Empty is the
    /// same rule entity falling and <see cref="HasGroundUnderFootprint"/> use, so nothing disagrees
    /// about which tiles are holes.
    /// </summary>
    private float TileGrip(EntityUid map, MapGridComponent mapGrid, int x, int y, ref bool contact)
    {
        if (!_map.TryGetTileRef(map, mapGrid, new Vector2i(x, y), out var tileRef) || tileRef.Tile.IsEmpty)
            return 0f;

        contact = true;
        return ((ContentTileDefinition) TilDefMan[tileRef.Tile.TypeId]).Friction;
    }
}
