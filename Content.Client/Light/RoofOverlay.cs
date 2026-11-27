using System.Numerics;
using Content.Client.Viewport;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Light;

public sealed partial class RoofOverlay : Overlay
{
    private static readonly ProtoId<TagPrototype> DiagonalTag = "Diagonal";
    private readonly IEntityManager _entManager;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IGameTiming _timing = default!;
    private readonly Dictionary<(EntityUid Grid, Vector2i Chunk), bool> _roofCandidates = new();
    private readonly HashSet<Entity<IsRoofComponent>> _roofEntities = new();
    private uint _roofCandidateFrame;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _mapSystem;
    private readonly SharedRoofSystem _roof = default!;
    private readonly SharedTransformSystem _xformSystem;
    private readonly TagSystem _tags;

    private List<Entity<MapGridComponent>> _grids = new();

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    public const int ContentZIndex = BeforeLightTargetOverlay.ContentZIndex + 1;

    public RoofOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        IoCManager.InjectDependencies(this);

        _lookup = _entManager.System<EntityLookupSystem>();
        _mapSystem = _entManager.System<SharedMapSystem>();
        _roof = _entManager.System<SharedRoofSystem>();
        _xformSystem = _entManager.System<SharedTransformSystem>();
        _tags = _entManager.System<TagSystem>();

        ZIndex = ContentZIndex;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is null or ScalingViewport.ZEye { PlanetTransitBackdrop: true } ||
            !_entManager.HasComponent<MapLightComponent>(args.MapUid))
            return;

        var viewport = args.Viewport;
        var eye = args.Viewport.Eye;

        var worldHandle = args.WorldHandle;
        var lightoverlay = _overlay.GetOverlay<BeforeLightTargetOverlay>();
        var bounds = lightoverlay.EnlargedBounds;
        var target = lightoverlay.GetCachedForViewport(viewport).EnlargedLightTarget;

        _grids.Clear();
        _mapSystem.FindGridsIntersecting(args.MapId, bounds, ref _grids, approx: true, includeMap: true);
        _grids.RemoveAll(static grid => grid.Comp.ChunkCount == 0);
        if (_grids.Count == 0)
            return;

        if (_roofCandidateFrame != _timing.CurFrame)
        {
            _roofCandidates.Clear();
            _roofCandidateFrame = _timing.CurFrame;
        }
        var lightScale = viewport.LightRenderTarget.Size / (Vector2) viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);

        var invMatrix = target.GetWorldToLocalMatrix(eye, scale);

        worldHandle.RenderInRenderTarget(target,
            () =>
            {
                for (var i = 0; i < _grids.Count; i++)
                {
                    var grid = _grids[i];

                    if (!_entManager.TryGetComponent(grid.Owner, out ImplicitRoofComponent? roof))
                        continue;

                    var gridMatrix = _xformSystem.GetWorldMatrix(grid.Owner);
                    var matty = Matrix3x2.Multiply(gridMatrix, invMatrix);

                    worldHandle.SetTransform(matty);

                    var tileEnumerator = _mapSystem.GetTilesEnumerator(grid.Owner, grid, bounds);
                    var color = roof.Color;

                    while (tileEnumerator.MoveNext(out var tileRef))
                    {
                        if (DrawDiagonalRoof(worldHandle, grid, tileRef.GridIndices, matty, color))
                            continue;

                        var local = _lookup.GetLocalBounds(tileRef, grid.Comp.TileSize);
                        worldHandle.DrawRect(local, color);
                    }

                    // Don't need it for the next stage.
                    _grids.RemoveAt(i);
                    i--;
                }
            }, null);

        worldHandle.RenderInRenderTarget(target,
            () =>
            {
                foreach (var grid in _grids)
                {
                    if (!_entManager.TryGetComponent(grid.Owner, out RoofComponent? roof))
                        continue;

                    var gridMatrix = _xformSystem.GetWorldMatrix(grid.Owner);
                    var matty = Matrix3x2.Multiply(gridMatrix, invMatrix);

                    worldHandle.SetTransform(matty);

                    DrawExplicitRoofs(worldHandle, grid, roof, bounds, matty);
                }
            }, null);

        worldHandle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawExplicitRoofs(DrawingHandleWorld handle, Entity<MapGridComponent> grid, RoofComponent roof,
        Box2Rotated bounds, Matrix3x2 gridToTarget)
    {
        const int chunkSize = RoofComponent.ChunkSize;
        var localBounds = _xformSystem.GetInvWorldMatrix(grid.Owner).TransformBox(bounds);
        var chunks = new ChunkIndicesEnumerator(localBounds, chunkSize);
        var tileSize = grid.Comp.TileSize;
        while (chunks.MoveNext(out var indices))
        {
            var chunk = indices.Value;
            var origin = chunk * chunkSize;
            var chunkBounds = Box2.FromDimensions(origin, new Vector2(chunkSize));
            var visible = localBounds.Intersect(chunkBounds);
            if (visible.Width <= 0f || visible.Height <= 0f)
                continue;

            var flags = roof.Data.GetValueOrDefault(chunk) | roof.ZLevelData.GetValueOrDefault(chunk);
            var entityRoofs = flags != ulong.MaxValue && HasRoofCandidates(grid, chunk, chunkBounds);
            if (flags == 0 && !entityRoofs)
                continue;

            var tiles = _mapSystem.GetLocalTilesEnumerator(grid.Owner, grid, visible);
            while (tiles.MoveNext(out var tile))
            {
                var index = tile.GridIndices;
                var relative = index - origin;
                var flagged = (flags & (1UL << (relative.X + relative.Y * chunkSize))) != 0;
                if (!flagged && !entityRoofs)
                    continue;

                var color = flagged ? roof.Color : _roof.GetColor((grid.Owner, grid.Comp, roof), index);
                if (color is not { } roofColor ||
                    !flagged && DrawDiagonalRoof(handle, grid, index, gridToTarget, roofColor, entityRoofOnly: true))
                    continue;

                handle.DrawRect(_lookup.GetLocalBounds(tile, tileSize), roofColor);
            }
        }
    }

    private bool HasRoofCandidates(Entity<MapGridComponent> grid, Vector2i chunk, Box2 chunkBounds)
    {
        var key = (grid.Owner, chunk);
        if (_roofCandidates.TryGetValue(key, out var hasRoof))
            return hasRoof;

        var size = grid.Comp.TileSize;
        var bounds = new Box2(chunkBounds.BottomLeft * size, chunkBounds.TopRight * size);
        _roofEntities.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid.Owner, bounds, _roofEntities,
            EntityLookupSystem.DefaultFlags | LookupFlags.Approximate);
        foreach (var ent in _roofEntities)
        {
            if (!ent.Comp.Enabled)
                continue;

            hasRoof = true;
            break;
        }

        _roofCandidates.Add(key, hasRoof);
        return hasRoof;
    }


    private bool DrawDiagonalRoof(DrawingHandleWorld handle, Entity<MapGridComponent> grid, Vector2i tile,
        Matrix3x2 gridToTarget, Color color, bool entityRoofOnly = false)
    {
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(grid.Owner, grid, tile);
        var drew = false;
        while (anchored.MoveNext(out var uid))
        {
            if (!_tags.HasTag(uid.Value, DiagonalTag) ||
                !_entManager.TryGetComponent(uid, out FixturesComponent? fixtures) ||
                !_entManager.TryGetComponent(uid, out TransformComponent? xform) ||
                (entityRoofOnly && (!_entManager.TryGetComponent(uid, out IsRoofComponent? roof) || !roof.Enabled)))
                continue;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard || fixture.Shape is not PolygonShape polygon)
                    continue;

                handle.SetTransform(xform.LocalMatrix * gridToTarget);
                var vertices = polygon.Vertices;
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, vertices.AsSpan(), color);
                drew = true;
            }
        }

        if (drew)
            handle.SetTransform(gridToTarget);
        return drew;
    }
}
