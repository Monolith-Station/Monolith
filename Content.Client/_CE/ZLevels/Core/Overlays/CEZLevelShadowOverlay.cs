/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client.Light;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.ZLevels.Core.Overlays;

public sealed partial class CEZLevelShadowOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _mapSystem;
    private readonly SharedTransformSystem _xformSystem;
    private readonly CESharedZLevelsSystem _zLevel;

    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<(Entity<MapGridComponent> Grid, float Height)> _shadowGrids = new();
    private readonly List<ShadowPlane> _shadowPlanes = new();
    private IRenderTexture? _shadowMask;

    public Color Color = Color.Black;

    public const float MaxShadowAlpha = 0.98f;
    public const float ShadowHeightFalloff = 0.45f;

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    public const int ContentZIndex = RoofOverlay.ContentZIndex + 1;

    public CEZLevelShadowOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        IoCManager.InjectDependencies(this);

        _lookup = _entManager.System<EntityLookupSystem>();
        _mapSystem = _entManager.System<SharedMapSystem>();
        _xformSystem = _entManager.System<SharedTransformSystem>();
        _zLevel = _entManager.System<CESharedZLevelsSystem>();

        ZIndex = ContentZIndex;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (eye == null || !_entManager.HasComponent<MapLightComponent>(args.MapUid) ||
            _entManager.HasComponent<SunShadowComponent>(args.MapUid))
            return;

        _shadowGrids.Clear();
        foreach (var plane in GetShadowPlanes(args.MapUid))
        {
            if (plane.Transit ? plane.Gap > 0.01f : plane.Gap > -1f)
                CollectShadowGrids(plane.Map, plane.Gap, args.WorldBounds, plane.Transit);
        }

        if (_shadowGrids.Count == 0)
            return;

        var viewport = args.Viewport;
        var worldHandle = args.WorldHandle;

        var lightOverlay = _overlay.GetOverlay<BeforeLightTargetOverlay>();
        var bounds = lightOverlay.EnlargedBounds;
        var target = lightOverlay.GetCachedForViewport(viewport).EnlargedLightTarget;

        if (_shadowMask?.Size != target.Size)
        {
            _shadowMask?.Dispose();
            _shadowMask = _clyde.CreateRenderTarget(target.Size, RenderTargetColorFormat.Rgba8, name: "z-shadow mask");
        }

        var mask = _shadowMask;
        var lightScale = viewport.LightRenderTarget.Size / (Vector2) viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);
        var invMatrix = mask.GetWorldToLocalMatrix(eye, scale);
        var overwrite = _prototypes.Index<ShaderPrototype>("CEZLightBufferCopy").Instance();

        // Overwrite distant silhouettes with nearer ones, then darken the light buffer once.
        _shadowGrids.Sort(static (a, b) => b.Height.CompareTo(a.Height));
        worldHandle.RenderInRenderTarget(mask,
            () =>
            {
                worldHandle.UseShader(overwrite);
                foreach (var (grid, height) in _shadowGrids)
                {
                    var alpha = MaxShadowAlpha * MathF.Exp(-ShadowHeightFalloff * height);
                    if (alpha <= 0.01f)
                        continue;

                    var color = Color.White.WithAlpha(alpha);

                    var gridMatrix = _xformSystem.GetWorldMatrix(grid.Owner);
                    var matty = Matrix3x2.Multiply(gridMatrix, invMatrix);
                    worldHandle.SetTransform(matty);

                    var tileEnumerator = _mapSystem.GetTilesEnumerator(grid.Owner, grid, bounds);
                    while (tileEnumerator.MoveNext(out var tileRef))
                    {
                        var tileDef = (ContentTileDefinition) _tileDefMan[tileRef.Tile.TypeId];
                        if (tileDef.Transparent)
                            continue;

                        var local = _lookup.GetLocalBounds(tileRef, grid.Comp.TileSize);
                        worldHandle.DrawRect(local, color);
                    }
                }
                worldHandle.UseShader(null);
            }, new Color(0f, 0f, 0f, 0f));

        worldHandle.SetTransform(Matrix3x2.Identity);
        var screen = args.RenderHandle.DrawingHandleScreen;
        screen.RenderInRenderTarget(target, () =>
        {
            screen.DrawTextureRect(mask.Texture, new UIBox2(Vector2.Zero, target.Size), Color);
        }, null);
    }

    internal readonly record struct ShadowPlane(EntityUid Map, float Gap, bool Transit);

    internal IReadOnlyList<ShadowPlane> GetShadowPlanes(EntityUid receiver)
    {
        _shadowPlanes.Clear();
        if (!TryGetShadowPlane(receiver, out var networkUid, out var altitude) ||
            !_entManager.TryGetComponent(networkUid, out CEZMapNetworkComponent? network))
            return _shadowPlanes;

        foreach (var map in network.SortedZLevels)
        {
            if (_entManager.TryGetComponent(map, out CEZMapComponent? deck) && deck.Depth + 2f > altitude)
                _shadowPlanes.Add(new ShadowPlane(map, deck.Depth - altitude, false));
        }

        var transits = _entManager.EntityQueryEnumerator<CEZTransitMapComponent>();
        while (transits.MoveNext(out var uid, out _))
        {
            if (TryGetShadowPlane(uid, out var transitNetwork, out var height) &&
                transitNetwork == networkUid && height + 1f > altitude)
                _shadowPlanes.Add(new ShadowPlane(uid, height - altitude, true));
        }

        return _shadowPlanes;
    }

    internal float GetCasterHeight(EntityUid uid, ShadowPlane plane) =>
        plane.Transit ? plane.Gap : plane.Gap + _zLevel.GetLocalAltitude(uid);

    private bool TryGetShadowPlane(EntityUid map, out EntityUid network, out float altitude)
    {
        if (_entManager.TryGetComponent(map, out CEZMapComponent? deck))
        {
            network = deck.NetworkUid;
            altitude = deck.Depth;
            return true;
        }

        if (_entManager.TryGetComponent(map, out CEZTransitMapComponent? transit))
        {
            var span = _zLevel.GetTransitSpan(transit);
            var height = _zLevel.GetTransitProgress(transit) * span;
            if (_entManager.TryGetComponent(transit.LowerMap, out deck))
            {
                network = deck.NetworkUid;
                altitude = deck.Depth + height;
                return true;
            }

            if (_entManager.TryGetComponent(transit.UpperMap, out deck))
            {
                network = deck.NetworkUid;
                altitude = deck.Depth + height - span;
                return true;
            }
        }

        network = default;
        altitude = default;
        return false;
    }

    private void CollectShadowGrids(EntityUid mapUid,
        float gap,
        Box2Rotated bounds,
        bool transit = false)
    {
        if (!_entManager.TryGetComponent(mapUid, out MapComponent? mapComp))
            return;

        _grids.Clear();
        _mapSystem.FindGridsIntersecting(mapComp.MapId, bounds, ref _grids, approx: true, includeMap: false);

        foreach (var grid in _grids)
        {
            var localPos = 0f;
            if (!transit && _entManager.TryGetComponent(grid.Owner, out CEZPhysicsComponent? zPhys))
                localPos = Math.Clamp(zPhys.LocalPosition, 0f, 1f);

            var height = gap + localPos;
            if (height > 0.01f)
                _shadowGrids.Add((grid, height));
        }
    }

    protected override void DisposeBehavior()
    {
        _shadowMask?.Dispose();
        base.DisposeBehavior();
    }
}
