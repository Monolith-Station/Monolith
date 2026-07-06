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

        // Depth of the nearest layer below the observer that caps the view, if any, with the
        // band under it that still renders. A cloud deck keeps a dissolve band (ships sink
        // through the tops); a ground layer is a hard floor you land ON, never below, so
        // nothing under it should ever draw. Transit ships past this are occluded and skip
        // their render pass (see the transit cull below).
        float? occludeBelowDepth = null;
        var occludeBand = 0f;

        // Standing on such a layer caps the whole view below the observer at once.
        if (_entityManager.HasComponent<CEZCloudLayerComponent>(playerMap))
        {
            occludeBelowDepth = ownDepth;
            occludeBand = CloudDissolveBand;
        }
        else if (_entityManager.HasComponent<CEZGroundLayerComponent>(playerMap))
        {
            occludeBelowDepth = ownDepth;
        }
        // Otherwise walk downward while there are empty tiles to see through. A cloud or
        // ground layer ends the walk: nothing beneath it is visible.
        else if (TryFindEmptyTiles(playerMap))
        {
            var current = belowChainStart;
            var depthCursor = belowChainStartDepth;
            for (var i = 0; i < CESharedZLevelsSystem.MaxZLevelsBelowRendering && current != null; i++)
            {
                _zPasses.Add((current.Value, depthCursor, false, false));

                if (_entityManager.HasComponent<CEZCloudLayerComponent>(current.Value))
                {
                    occludeBelowDepth = depthCursor;
                    occludeBand = CloudDissolveBand;
                    break;
                }

                if (_entityManager.HasComponent<CEZGroundLayerComponent>(current.Value))
                {
                    occludeBelowDepth = depthCursor;
                    break;
                }

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
        if (_entityManager.HasComponent<CEZMapComponent>(altitudeAnchor))
        {
            var observerAltitude = _zLevels.GetAbsoluteAltitude(_player.LocalEntity.Value);
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

                // Transit grid's absolute altitude (lower-anchor depth + gap progress, folded in by
                // the API) relative to the observer's. Fall back to the gap's lower plane if the map
                // has no primary grid to read.
                var transitAltitude = transit.PrimaryGrid is { } primaryGrid
                    ? _zLevels.GetAbsoluteAltitude(primaryGrid)
                    : lowerZ.Depth;
                var transitDepth = transitAltitude - observerAltitude;

                // Hidden under a layer between it and the observer: for a cloud, ships still
                // within the dissolve band show through (as sinking ghosts) so only deeper
                // ones are culled; for a ground layer the band is zero, so anything below the
                // floor is dropped outright. This is what lets a deck cheapen the scene
                // without hiding ships still crossing it.
                if (occludeBelowDepth is { } occludeDepth && transitDepth < occludeDepth - occludeBand)
                    continue;

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

        // Builds the perspective eye for a pass at an arbitrary depth. Factored out of the
        // main loop's inline construction so a ship that has sunk below a cloud deck can be
        // re-projected and re-drawn OVER the deck (see the cloudDeck branch).
        ZEye? MakeZEye(EntityUid targetMap, float d)
        {
            if (_fallbackEye is null || !_mapQuery.Value.TryComp(targetMap, out var mapComp))
                return null;

            Angle rot = _fallbackEye.Rotation * -1;
            var off = rot.ToWorldVec() * CEClientZLevelsSystem.ZLevelOffset * (d - ownDepth);
            var scale = MathF.Pow(CESharedZLevelsSystem.ZLevelViewShrink, -d);

            return new ZEye(lowestDepth, d, highestDepth)
            {
                Position = new MapCoordinates(_fallbackEye.Position.Position, mapComp.MapId),
                DrawFov = false,
                DrawLight = _fallbackEye.DrawLight,
                DrawParallax = false,
                Offset = _fallbackEye.Offset + off,
                Rotation = _fallbackEye.Rotation,
                Scale = _fallbackEye.Scale * scale,
            };
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
                    // For overlay guards and other random shit that should only ever run on the actual eye and not the rest.
                    Primary = mapUid == playerMap && !isTransit,
                    DrawFov = _fallbackEye.DrawFov && allowFov,
                    DrawLight = _fallbackEye.DrawLight,
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

                // Clouds are an opaque layer. Rendering hack so that transit maps still render right when a ship goes below the clouds.
                foreach (var (sinkMap, sinkDepth, _, sinkTransit) in _zPasses)
                {
                    if (!sinkTransit)
                        continue;

                    var below = depth - sinkDepth; // how far this ship sits below the deck
                    if (below <= 0f || below >= CloudDissolveBand)
                        continue;

                    if (MakeZEye(sinkMap, sinkDepth) is { } sinkEye)
                    {
                        var t = below / CloudDissolveBand;
                        BlitTransitCloudGhost(renderHandle, viewport, sinkEye, cloudDeck.CloudColor, tint: t, alpha: 1f - t);
                    }
                }
                if (mapUid == playerMap && depth <= 0.001f)
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
        if (_entityManager.TryGetComponent(transitMap, out CEZTransitMapComponent? transit))
        {
            var progress = GetTransitProgress(transit);

            if (transit.UpperMap is { } upper &&
                _entityManager.TryGetComponent(upper, out CEZCloudLayerComponent? cloudAbove))
            {
                cloud = CloudCoverage(1f - progress);
                cloudColor = new Vector3(cloudAbove.CloudColor.R, cloudAbove.CloudColor.G, cloudAbove.CloudColor.B);
            }

            if (transit.LowerMap is { } lower &&
                _entityManager.TryGetComponent(lower, out CEZCloudLayerComponent? cloudBelow))
            {
                var belowCover = CloudCoverage(progress);
                if (belowCover > cloud)
                {
                    cloud = belowCover;
                    cloudColor = new Vector3(cloudBelow.CloudColor.R, cloudBelow.CloudColor.G, cloudBelow.CloudColor.B);
                }
            }
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
    /// Renders a transit map to the scratch viewport under <paramref name="eye"/> and blits
    /// just its hull over the main target: its own colors tinted toward
    /// <paramref name="cloudColor"/> by <paramref name="tint"/> (0 = untouched hull, 1 = flat
    /// deck color) and drawn at <paramref name="alpha"/>. Used to re-draw a ship that has sunk
    /// below a cloud deck — and been hidden by its opaque fill — as a fogging, fading hull
    /// sinking into the clouds instead of vanishing on the spot.
    /// </summary>
    private void BlitTransitCloudGhost(IRenderHandle renderHandle, IClydeViewport viewport, IEye? eye, Color cloudColor, float tint, float alpha)
    {
        if (eye is null || alpha <= 0.001f)
            return;

        if (_transitViewport == null || _transitViewport.Size != viewport.Size)
        {
            _transitViewport?.Dispose();
            _transitViewport = _clyde.CreateViewport(viewport.Size, nameof(_transitViewport));
            _transitViewport.RenderScale = viewport.RenderScale;
        }

        _transitBlitShader ??= _prototypeManager.Index<ShaderPrototype>("CEZBlurBlit").InstanceUnique();

        _transitViewport.Eye = eye;
        _transitViewport.ClearColor = new Color(0f, 0f, 0f, 0f);
        _transitViewport.Render();

        var col = new Vector3(cloudColor.R, cloudColor.G, cloudColor.B);
        var screenHandle = renderHandle.DrawingHandleScreen;
        screenHandle.RenderInRenderTarget(viewport.RenderTarget, () =>
        {
            var texture = _transitViewport.RenderTarget.Texture;

            _transitBlitShader.SetParameter("BLUR_COLOR", new Vector3(0f, 0f, 0f));
            _transitBlitShader.SetParameter("STRENGTH", 0f);
            _transitBlitShader.SetParameter("CLOUD_COLOR", col);
            _transitBlitShader.SetParameter("CLOUD", Math.Clamp(tint, 0f, 1f));
            _transitBlitShader.SetParameter("FADE", Math.Clamp(alpha, 0f, 1f));

            screenHandle.UseShader(_transitBlitShader);
            screenHandle.DrawTextureRect(texture, new UIBox2(Vector2.Zero, texture.Size));
            screenHandle.UseShader(null);
        }, null);
    }

    /// <summary>
    /// Levels of descent over which a below-observer ship dissolves into a cloud deck
    /// beneath it: full whiteout at the plane, clear this far above.
    /// </summary>
    private const float CloudDissolveBand = 0.5f;

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

        /// <summary>
        /// Marker for which eye the player sees as their "primary" one (the one their entity is on).
        /// This is here to make anyone messing with screenspace overlay's lives easier.
        ///
        /// (You should add a guard like the one in Vignette if it applies to every eye.)
        /// </summary>
        public bool Primary;
    }
}
