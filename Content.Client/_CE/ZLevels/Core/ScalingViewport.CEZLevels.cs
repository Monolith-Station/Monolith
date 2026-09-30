/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private CEClientZLevelsSystem? _zLevels;
    private SharedMapSystem? _mapSystem;

    private EntityQuery<TransformComponent>? _xformQuery;
    private EntityQuery<MapComponent>? _mapQuery;

    private IEye? _fallbackEye;

    /// <summary>
    /// We are looking for at least one empty tile on the screen.
    /// This is used to ensure that it makes sense to draw the z-planes and that they are visible.
    /// </summary>
    public bool TryFindEmptyTiles(EntityUid mapUid)
    {
        if (_xformQuery is null || !_xformQuery.Value.TryComp(mapUid, out var xform))
            return true;

        var drawBox = GetDrawBox();
        var mapId = xform.MapID;

        var corners = new[]
        {
            _eyeManager.ScreenToMap(drawBox.BottomLeft).Position,
            _eyeManager.ScreenToMap(drawBox.BottomRight).Position,
            _eyeManager.ScreenToMap(drawBox.TopLeft).Position,
            _eyeManager.ScreenToMap(drawBox.TopRight).Position
        };

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var c in corners)
        {
            if (c.X < minX)
                minX = c.X;
            if (c.Y < minY)
                minY = c.Y;
            if (c.X > maxX)
                maxX = c.X;
            if (c.Y > maxY)
                maxY = c.Y;
        }

        var mapCoordsBottomLeft = new MapCoordinates(new Vector2(minX, minY), mapId);
        var mapCoordsTopRight = new MapCoordinates(new Vector2(maxX, maxY), mapId);

        if (_mapSystem is null || !_mapManager.TryFindGridAt(mapUid, mapCoordsBottomLeft.Position, out var gridUid, out var grid))
            return true;

        var tileBottomLeft = _mapSystem.TileIndicesFor(gridUid, grid, mapCoordsBottomLeft);
        var tileTopRight = _mapSystem.TileIndicesFor(gridUid, grid, mapCoordsTopRight);

        for (var x = tileBottomLeft.X - 1; x <= tileTopRight.X + 1; x++)
        {
            for (var y = tileBottomLeft.Y - 1; y <= tileTopRight.Y + 1; y++)
            {
                var tile = _mapSystem.GetTileRef(gridUid, grid, new Vector2i(x, y));
                var tileDef = (ContentTileDefinition)_tile[tile.Tile.TypeId];
                if (tileDef.Transparent || tile.Tile.IsEmpty)
                    return true;
            }
        }

        return false;
    }

    private readonly List<(EntityUid MapUid, float Depth, bool AllowFov, bool Transit)> _zPasses = new();
    private IClydeViewport? _transitViewport;
    private ShaderInstance? _transitBlitShader;
    private ShaderInstance? _cloudShader;

    private void RenderZLevels(IRenderHandle renderHandle, IClydeViewport viewport)
    {
        if (_eye is null)
            return;

        _fallbackEye = _eye;

        // Cache frequently accessed components/systems
        _xformQuery ??= _entityManager.GetEntityQuery<TransformComponent>();
        _mapQuery ??= _entityManager.GetEntityQuery<MapComponent>();

        // Cache systems and components
        _zLevels ??= _entityManager.System<CEClientZLevelsSystem>();
        _mapSystem ??= _entityManager.System<SharedMapSystem>();

        if (_player.LocalEntity is null)
            return;

        if (!_entityManager.TryGetComponent<CEZLevelViewerComponent>(_player.LocalEntity.Value, out var zLevelViewer))
            return;

        if (!_xformQuery.Value.TryComp(_player.LocalEntity, out var playerXform))
            return;

        if (playerXform.MapUid is null)
            return;

        var playerMap = playerXform.MapUid.Value;

        _zPasses.Clear();

        var frac = 0f;
        var ownDepth = 0f;
        EntityUid? belowChainStart = null;
        var belowChainStartDepth = -1f;
        EntityUid? aboveMap = null;
        var aboveDepth = 1f;

        if (_entityManager.TryGetComponent(playerMap, out CEZTransitMapComponent? riderTransit))
        {
            frac = GetTransitProgress(riderTransit);
            belowChainStart = riderTransit.LowerMap;
            belowChainStartDepth = -frac;
            aboveMap = riderTransit.UpperMap;
            aboveDepth = 1f - frac;
        }
        else
        {
            frac = _zLevels.GetLocalAltitude(_player.LocalEntity.Value);
            ownDepth = -frac;
            belowChainStartDepth = -1f - frac;
            aboveDepth = 1f - frac;

            if (_zLevels.TryMapOffset(playerMap, -1, out var mapBelow))
                belowChainStart = mapBelow.Owner;
            if (_zLevels.TryMapUp(playerMap, out var mapAbove))
                aboveMap = mapAbove.Owner;
        }

        if (TryFindEmptyTiles(playerMap) &&
            !_entityManager.HasComponent<CEZCloudLayerComponent>(playerMap))
        {
            var current = belowChainStart;
            var depthCursor = belowChainStartDepth;
            for (var i = 0; i < CESharedZLevelsSystem.MaxZLevelsBelowRendering && current != null; i++)
            {
                _zPasses.Add((current.Value, depthCursor, false, false));

                if (_entityManager.HasComponent<CEZCloudLayerComponent>(current.Value))
                    break; // clouds are very hard to see through

                if (!TryFindEmptyTiles(current.Value))
                    break;

                current = _zLevels.TryMapOffset(current.Value, -1, out var next) ? next.Owner : null;
                depthCursor -= 1f;
            }
        }

        // always render your own map
        _zPasses.Add((playerMap, ownDepth, true, false));

        if (riderTransit != null)
        {
            if (aboveMap != null && aboveDepth > 0.001f && TransitFade(aboveDepth) > 0.01f)
                _zPasses.Add((aboveMap.Value, aboveDepth, false, true));
        }
        else if (zLevelViewer.LookUp && aboveMap != null)
        {
            _zPasses.Add((aboveMap.Value, aboveDepth, true, false));
        }

        // transit maps also render
        var altitudeAnchor = riderTransit?.LowerMap ?? playerMap;
        if (_entityManager.TryGetComponent(altitudeAnchor, out CEZMapComponent? anchorZ))
        {
            var observerAltitude = anchorZ.Depth + frac;
            var hasObserverNetwork = _zLevels.TryGetMapNetwork(altitudeAnchor, out var observerNetwork);

            var transitQuery = _entityManager.EntityQueryEnumerator<CEZTransitMapComponent>();
            while (transitQuery.MoveNext(out var transitUid, out var transit))
            {
                if (transitUid == playerMap || transit.LowerMap is not { } lowerMap)
                    continue;

                if (!_entityManager.TryGetComponent(lowerMap, out CEZMapComponent? lowerZ))
                    continue;

                if (hasObserverNetwork &&
                    (!_zLevels.TryGetMapNetwork(lowerMap, out var transitNetwork) ||
                     transitNetwork.Owner != observerNetwork.Owner))
                {
                    continue;
                }

                var transitDepth = lowerZ.Depth + GetTransitProgress(transit) - observerAltitude;

                // they're gone
                if (transitDepth > 0f && TransitFade(transitDepth) <= 0.01f)
                    continue;

                _zPasses.Add((transitUid, transitDepth, false, true));
            }
        }

        // Painter's algorithm.
        _zPasses.Sort(static (a, b) =>
        {
            var aUp = a.Depth > 0f;
            var bUp = b.Depth > 0f;
            if (aUp != bUp)
                return aUp ? 1 : -1;
            return aUp ? b.Depth.CompareTo(a.Depth) : a.Depth.CompareTo(b.Depth);
        });

        CEZCloudLayerComponent? riderDeck = null;
        if (aboveMap != null &&
            aboveDepth <= CloudFullCoverDepth &&
            _entityManager.TryGetComponent(aboveMap.Value, out CEZCloudLayerComponent? riderDeckComp))
        {
            riderDeck = riderDeckComp;
        }

        var lowestDepth = float.MaxValue;
        var highestDepth = float.MinValue;
        foreach (var pass in _zPasses)
        {
            lowestDepth = Math.Min(lowestDepth, pass.Depth);
            highestDepth = Math.Max(highestDepth, pass.Depth);
        }
        var first = true;

        foreach (var (mapUid, depth, allowFov, isTransit) in _zPasses)
        {
            // A cloud layer at or below the observer draws an opaque deck beneath
            // its own pass: deeper passes already rendered vanish under it, grids
            // parked on the layer draw crisp on top of it.
            CEZCloudLayerComponent? cloudDeck = null;
            if (depth <= 0.001f && !isTransit)
                _entityManager.TryGetComponent(mapUid, out cloudDeck);

            if (mapUid == playerMap && depth == 0f)
            {
                viewport.Eye = _fallbackEye;
            }
            else
            {
                if (!_mapQuery.Value.TryComp(mapUid, out var mapComp))
                    continue;

                Angle rotation = _fallbackEye.Rotation * -1;

                var offset = rotation.ToWorldVec() * CEClientZLevelsSystem.ZLevelOffset * (depth - ownDepth);
                var zScale = MathF.Pow(CESharedZLevelsSystem.ZLevelViewShrink, -depth);

                var zEye = new ZEye(lowestDepth, depth, highestDepth)
                {
                    Position = new MapCoordinates(_fallbackEye.Position.Position, mapComp.MapId),
                    // Not gated on depth >= 0: an airborne viewer's own map sits at a
                    // small negative depth but their walls still block sight.
                    DrawFov = _fallbackEye.DrawFov && allowFov,
                    DrawLight = _fallbackEye.DrawLight,
                    // A pass with a cloud deck never wants the skybox: the deck IS
                    // the backdrop, and parallax would paint over it.
                    DrawParallax = !isTransit && depth == lowestDepth && cloudDeck == null,
                    Offset = _fallbackEye.Offset + offset,
                    Rotation = _fallbackEye.Rotation,
                    Scale = _fallbackEye.Scale * zScale,
                };

                if (isTransit && depth > 0f)
                {
                    RenderTransitOverhead(renderHandle, viewport, mapUid, zEye, depth);
                    continue;
                }

                viewport.Eye = zEye;
            }


            Color? wispColor = null;

            if (riderDeck != null && mapUid == playerMap && !isTransit && depth == ownDepth)
            {
                DrawCloudDeck(renderHandle, viewport, riderDeck.CloudColor, 1f);
                DrawCloudWisps(renderHandle, viewport, riderDeck.CloudColor);
                first = false;
            }


            else if (cloudDeck != null)
            {
                DrawCloudDeck(renderHandle, viewport, cloudDeck.CloudColor, 1f);

                if (mapUid == playerMap && depth == 0f)
                    DrawCloudWisps(renderHandle, viewport, cloudDeck.CloudColor);

                else if (depth > -0.5f)
                    wispColor = cloudDeck.CloudColor;
                first = false;
            }

            viewport.ClearColor = first ? Color.Black : null;
            first = false;
            viewport.Render();

            if (wispColor != null)
                DrawCloudWisps(renderHandle, viewport, wispColor.Value);
        }

        if (aboveMap != null &&
            _entityManager.TryGetComponent(aboveMap.Value, out CEZCloudLayerComponent? cloudAbove))
        {
            var coverage = CloudCoverage(aboveDepth);
            if (coverage > 0.001f)
                DrawCloudDeck(renderHandle, viewport, cloudAbove.CloudColor, coverage);
        }

        // Restore the Eye
        Eye = _fallbackEye;
        viewport.Eye = Eye;
    }

    private void RenderTransitOverhead(IRenderHandle renderHandle,
        IClydeViewport viewport,
        EntityUid transitMap,
        ZEye zEye,
        float depth)
    {
        if (_transitViewport == null || _transitViewport.Size != viewport.Size)
        {
            _transitViewport?.Dispose();
            _transitViewport = _clyde.CreateViewport(viewport.Size, nameof(_transitViewport));
            _transitViewport.RenderScale = viewport.RenderScale;
        }

        _transitBlitShader ??= _prototypeManager.Index<ShaderPrototype>("CEZBlurBlit").InstanceUnique();

        zEye.DrawParallax = false;

        _transitViewport.Eye = zEye;
        // "Why aren't you using Color.Transparent" because it's LIES it is entirely white
        _transitViewport.ClearColor = new Color(0f, 0f, 0f, 0f);
        _transitViewport.Render();

        var hazeColor = new Vector3(0, 0, 1);
        if (_entityManager.TryGetComponent(transitMap, out MapLightComponent? mapLight))
        {
            hazeColor = new Vector3(
                mapLight.AmbientLightColor.R,
                mapLight.AmbientLightColor.G,
                mapLight.AmbientLightColor.B);
        }

        var strength = Math.Clamp(depth, 0f, 1f);

        var cloud = 0f;
        var cloudColor = Vector3.One;
        if (_entityManager.TryGetComponent(transitMap, out CEZTransitMapComponent? transit) &&
            transit.UpperMap is { } upper &&
            _entityManager.TryGetComponent(upper, out CEZCloudLayerComponent? cloudLayer))
        {
            cloud = CloudCoverage(1f - GetTransitProgress(transit));
            cloudColor = new Vector3(cloudLayer.CloudColor.R, cloudLayer.CloudColor.G, cloudLayer.CloudColor.B);
        }

        var screenHandle = renderHandle.DrawingHandleScreen;
        screenHandle.RenderInRenderTarget(viewport.RenderTarget, () =>
        {
            var texture = _transitViewport.RenderTarget.Texture;

            _transitBlitShader.SetParameter("BLUR_COLOR", hazeColor);
            _transitBlitShader.SetParameter("STRENGTH", strength);
            _transitBlitShader.SetParameter("CLOUD_COLOR", cloudColor);
            _transitBlitShader.SetParameter("CLOUD", cloud);
            _transitBlitShader.SetParameter("FADE", TransitFade(depth));

            screenHandle.UseShader(_transitBlitShader);
            screenHandle.DrawTextureRect(texture, new UIBox2(Vector2.Zero, texture.Size));
            screenHandle.UseShader(null);
        }, null);
    }

    /// <summary>
    /// How many z-levels of climb it takes for a transiting ship seen from below to
    /// fully dissolve into the sky.
    /// </summary>
    public const float TransitFadeDepth = 0.8f;

    private static float TransitFade(float depth)
    {
        return Math.Clamp(1f - depth / TransitFadeDepth, 0f, 1f);
    }

    public const float CloudFullCoverDepth = 0.25f;

    private const float CloudBreakthroughBand = 0.1f;

    /// <summary>
    /// Cloud coverage over a grid by its depth below a cloud layer
    /// </summary>
    private static float CloudCoverage(float depthBelowLayer)
    {
        if (depthBelowLayer <= 0f)
            return 0f;

        if (depthBelowLayer >= CloudFullCoverDepth)
            return Math.Clamp((1f - depthBelowLayer) / (1f - CloudFullCoverDepth), 0f, 1f);

        return Math.Clamp(
            (depthBelowLayer - (CloudFullCoverDepth - CloudBreakthroughBand)) / CloudBreakthroughBand,
            0f,
            1f);
    }

    /// <summary>
    /// Draw clouds.
    /// </summary>
    private void DrawCloudDeck(IRenderHandle renderHandle, IClydeViewport viewport, Color color, float coverage)
    {
        _cloudShader ??= _prototypeManager.Index<ShaderPrototype>("CEZClouds").InstanceUnique();

        var screenHandle = renderHandle.DrawingHandleScreen;
        screenHandle.RenderInRenderTarget(viewport.RenderTarget, () =>
        {
            _cloudShader.SetParameter("CLOUD_COLOR", new Vector3(color.R, color.G, color.B));
            _cloudShader.SetParameter("COVERAGE", coverage);
            _cloudShader.SetParameter("WISP", 0f);

            screenHandle.UseShader(_cloudShader);
            screenHandle.DrawRect(new UIBox2(Vector2.Zero, viewport.RenderTarget.Texture.Size), Color.White);
            screenHandle.UseShader(null);
        }, null);
    }

    private void DrawCloudWisps(IRenderHandle renderHandle, IClydeViewport viewport, Color color)
    {
        _cloudShader ??= _prototypeManager.Index<ShaderPrototype>("CEZClouds").InstanceUnique();

        var screenHandle = renderHandle.DrawingHandleScreen;
        screenHandle.RenderInRenderTarget(viewport.RenderTarget, () =>
        {
            _cloudShader.SetParameter("CLOUD_COLOR", new Vector3(color.R, color.G, color.B));
            _cloudShader.SetParameter("COVERAGE", 0f);
            _cloudShader.SetParameter("WISP", 0.85f);

            screenHandle.UseShader(_cloudShader);
            screenHandle.DrawRect(new UIBox2(Vector2.Zero, viewport.RenderTarget.Texture.Size), Color.White);
            screenHandle.UseShader(null);
        }, null);
    }

    private float GetTransitProgress(CEZTransitMapComponent transit)
    {
        if (transit.PrimaryGrid is { } grid &&
            _entityManager.TryGetComponent(grid, out CEZPhysicsComponent? zPhys))
        {
            return Math.Clamp(zPhys.LocalPosition, 0f, 1f);
        }

        return 0f;
    }

    public sealed class ZEye(float lowest, float depth, float high) : Robust.Shared.Graphics.Eye
    {
        public float LowestDepth = lowest;
        public float Depth = depth;
        public float HighestDepth = high;

        /// <summary>
        /// whether parallax draws (only used on the actual bottom layer of a z stack so transit doesnt explode time)
        /// </summary>
        public bool DrawParallax = true;
    }
}
