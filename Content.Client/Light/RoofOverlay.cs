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

namespace Content.Client.Light;

public sealed partial class RoofOverlay : Overlay
{
    private static readonly ProtoId<TagPrototype> DiagonalTag = "Diagonal";
    private readonly IEntityManager _entManager;
    [Dependency] private IOverlayManager _overlay = default!;

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

                    var tileEnumerator = _mapSystem.GetTilesEnumerator(grid.Owner, grid, bounds);
                    var roofEnt = (grid.Owner, grid.Comp, roof);

                    // Due to stencilling we essentially draw on unrooved tiles
                    while (tileEnumerator.MoveNext(out var tileRef))
                    {
                        var color = _roof.GetColor(roofEnt, tileRef.GridIndices);

                        if (color == null)
                        {
                            continue;
                        }

                        if (!SharedRoofSystem.HasRoofFlag(roof, tileRef.GridIndices) &&
                            DrawDiagonalRoof(worldHandle, grid, tileRef.GridIndices, matty, color.Value, entityRoofOnly: true))
                            continue;

                        var local = _lookup.GetLocalBounds(tileRef, grid.Comp.TileSize);
                        worldHandle.DrawRect(local, color.Value);
                    }
                }
            }, null);

        worldHandle.SetTransform(Matrix3x2.Identity);
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
