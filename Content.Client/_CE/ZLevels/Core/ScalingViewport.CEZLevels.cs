/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using System.Linq;
using Content.Client._CE.Planets.Shields;
using Content.Client._CE.ZLevels.Core;
using Content.Client._Mono.ShipShieldShell; // Mono
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    private CEClientZLevelsSystem? _zLevels;
    private SharedMapSystem? _mapSystem;

    private EntityQuery<TransformComponent>? _xformQuery;
    private EntityQuery<MapComponent>? _mapQuery;

    private IEye? _fallbackEye;

    // Mono Start
    private IEye? _lookUpAimEye;
    private MapId _lookUpSourceMap;

    internal bool TryGetLookUpAim(Vector2 screenPosition, MapId sourceMap, out MapCoordinates aim)
    {
        aim = default;
        if (_lookUpAimEye is not { } eye || sourceMap != _lookUpSourceMap)
            return false;

        aim = PixelToMap(screenPosition, eye);
        return aim.MapId != MapId.Nullspace;
    }
    // Mono end

    /// <summary>
    /// We are looking for at least one empty tile on the screen.
    /// This is used to ensure that it makes sense to draw the z-planes and that they are visible.
    /// </summary>
    private bool TryFindEmptyTiles(EntityUid mapUid, IClydeViewport viewport, IEye? eye)
    {
        if (_mapSystem is null || eye is null ||
            !Matrix3x2.Invert(viewport.RenderTarget.GetWorldToLocalMatrix(eye, viewport.RenderScale), out var toWorld))
            return true;

        var pixels = Box2.FromDimensions(Vector2.Zero, viewport.Size);
        var corner = Vector2.Transform(pixels.BottomLeft, toWorld);
        if (_mapSystem is null || !_mapSystem.TryFindGridAt(mapUid, corner, out var gridUid, out var grid))
            return true;

        var transforms = _entityManager.System<SharedTransformSystem>();
        var bounds = (toWorld * transforms.GetInvWorldMatrix(gridUid)).TransformBox(pixels);
        var min = bounds.BottomLeft / grid.TileSize;
        var max = bounds.TopRight / grid.TileSize;

        for (var x = (int) MathF.Floor(min.X) - 1; x <= (int) MathF.Floor(max.X) + 1; x++)
        {
            for (var y = (int) MathF.Floor(min.Y) - 1; y <= (int) MathF.Floor(max.Y) + 1; y++)
            {
                var tile = _mapSystem.GetTileRef(gridUid, grid, new Vector2i(x, y));
                if (tile.Tile.IsEmpty)
                    return true;

                var tileDef = (ContentTileDefinition)_tile[tile.Tile.TypeId];
                if (tileDef.Transparent)
                    return true;
            }
        }

        return false;
    }

    private readonly List<(EntityUid MapUid, float Depth, bool AllowFov, bool Transit)> _zPasses = new();
    private IClydeViewport? _transitViewport;
    private ShaderInstance? _zDeckComposite;
    private ShaderInstance? _transitBlitShader;
    private ShaderInstance? _cloudShader;

    internal static (float Scale, float Offset) ZProjection(float depth, float anchorDepth = 0f) // Mono: Shared with the ship shield renderer.
    {
        const float shrink = CESharedZLevelsSystem.ZLevelViewShrink;
        var relativeDepth = depth - anchorDepth;
        return shrink == 1f
            ? (1f, CESharedZLevelsSystem.ZLevelOffset * relativeDepth)
            : (MathF.Pow(shrink, -depth),
                CESharedZLevelsSystem.ZLevelOffset * (1f - MathF.Pow(shrink, relativeDepth)) / (1f - shrink));
    }

    private void RenderZLevels(IRenderHandle renderHandle, IClydeViewport viewport)
    {
        _lookUpAimEye = null; // Mono
        if (_eye is null)
            return;

        _fallbackEye = _eye;

        _xformQuery ??= _entityManager.GetEntityQuery<TransformComponent>();
        _mapQuery ??= _entityManager.GetEntityQuery<MapComponent>();

        _zLevels ??= _entityManager.System<CEClientZLevelsSystem>();
        _mapSystem ??= _entityManager.System<SharedMapSystem>();

        if (_player.LocalEntity is not { } observer ||
            !_entityManager.TryGetComponent<CEZLevelViewerComponent>(observer, out var zLevelViewer))
        {
            RenderFallback(viewport);
            return;
        }

        var cameraAnchor = observer;
        if (_entityManager.TryGetComponent<EyeComponent>(observer, out var eyeComponent) &&
            ReferenceEquals(eyeComponent.Eye, _eye) && eyeComponent.Target is { } eyeTarget)
            cameraAnchor = eyeTarget;

        if (!_xformQuery.Value.TryComp(cameraAnchor, out var playerXform) ||
            playerXform.MapUid is not { } playerMap ||
            playerXform.MapID != _eye.Position.MapId)
        {
            RenderFallback(viewport);
            return;
        }

        if (cameraAnchor == observer)
            cameraAnchor = _zLevels.GetCameraAltitudeAnchor(playerXform);

        if (_entityManager.TryGetComponent<PlanetTransitMapComponent>(playerMap, out var planetMap))
        {
            RenderPlanetTransitRider(renderHandle, viewport, playerMap, planetMap);
            return;
        }

        if (!_entityManager.HasComponent<CEZMapComponent>(playerMap) &&
            !_entityManager.HasComponent<CEZTransitMapComponent>(playerMap))
        {
            RenderFallback(viewport);
            return;
        }

        var observerAltitude = _zLevels.GetAbsoluteAltitude(cameraAnchor);
        _zPasses.Clear();

        var frac = 0f;
        var ownDepth = 0f;
        EntityUid? belowChainStart = null;
        var belowChainStartDepth = -1f;
        EntityUid? aboveMap = null;
        var aboveDepth = 1f;
        var lowestDepth = float.MaxValue;
        var highestDepth = float.MinValue;

        if (_entityManager.TryGetComponent(playerMap, out CEZTransitMapComponent? riderTransit))
        {
            var span = _zLevels.GetTransitSpan(riderTransit);
            frac = GetTransitProgress(riderTransit) * span;
            belowChainStart = riderTransit.LowerMap;
            belowChainStartDepth = -frac;
            aboveMap = riderTransit.UpperMap;
            aboveDepth = span - frac;
        }
        else
        {
            frac = _zLevels.GetLocalAltitude(cameraAnchor);
            ownDepth = -frac;
            belowChainStartDepth = -1f - frac;
            aboveDepth = 1f - frac;

            if (_zLevels.TryMapOffset(playerMap, -1, out var mapBelow))
                belowChainStart = mapBelow.Owner;
            if (_zLevels.TryMapUp(playerMap, out var mapAbove))
                aboveMap = mapAbove.Owner;
        }

        // Clouds retain a dissolve band below the view cap; solid ground does not.
        float? occludeBelowDepth = null;
        EntityUid? backdropCloudMap = null;
        var riderDeck = aboveDepth <= CloudFullCoverDepth
            ? _entityManager.GetComponentOrNull<CEZCloudLayerComponent>(aboveMap)
            : null;

        if (riderDeck != null)
        {
            occludeBelowDepth = ownDepth;
        }
        else if (_entityManager.HasComponent<CEZCloudLayerComponent>(playerMap))
        {
            occludeBelowDepth = ownDepth;
            backdropCloudMap = playerMap;
        }
        else if (_entityManager.HasComponent<CEZGroundLayerComponent>(playerMap))
        {
            occludeBelowDepth = ownDepth;
        }
        // Walk down through visible gaps until a cloud or ground layer occludes the view.
        else if (riderTransit != null || TryFindEmptyTiles(playerMap, viewport, MakeZEye(playerMap, ownDepth)))
        {
            var current = belowChainStart;
            var depthCursor = belowChainStartDepth;
            for (var i = 0; i < CESharedZLevelsSystem.MaxZLevelsBelowRendering && current != null; i++)
            {
                _zPasses.Add((current.Value, depthCursor, false, false));

                if (_entityManager.HasComponent<CEZCloudLayerComponent>(current.Value))
                {
                    occludeBelowDepth = depthCursor;
                    backdropCloudMap = current;
                    break;
                }

                if (_entityManager.HasComponent<CEZGroundLayerComponent>(current.Value))
                {
                    occludeBelowDepth = depthCursor;
                    break;
                }

                if (!TryFindEmptyTiles(current.Value, viewport, MakeZEye(current.Value, depthCursor)))
                    break;

                current = _zLevels.TryMapOffset(current.Value, -1, out var next) ? next.Owner : null;
                depthCursor -= 1f;
            }
        }

        _zPasses.Add((playerMap, ownDepth, true, false));

        var visibleAbove = zLevelViewer.LookUp ? _zLevels.GetVisibleZLevelsAbove(cameraAnchor, playerMap) : 0;
        var overheadViewDepth = 0f;
        for (var i = 1; i <= visibleAbove; i++)
        {
            if (!_zLevels.TryMapOffset(playerMap, i, out var upper))
                break;

            overheadViewDepth = aboveDepth + i - 1;
            _zPasses.Add((upper.Owner, overheadViewDepth, true, false));
        }

        if (visibleAbove == 0 && riderTransit != null && aboveMap != null &&
            aboveDepth >= 0f && TransitFade(aboveDepth) > 0.01f)
            _zPasses.Add((aboveMap.Value, aboveDepth, false, true));

        var altitudeAnchor = riderTransit?.LowerMap ?? playerMap;
        if (_entityManager.HasComponent<CEZMapComponent>(altitudeAnchor))
        {
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

                var transitAltitude = transit.PrimaryGrid is { } primaryGrid
                    ? _zLevels.GetAbsoluteAltitude(primaryGrid)
                    : lowerZ.Depth;
                var transitDepth = transitAltitude - observerAltitude;

                var dissolveBand = backdropCloudMap != null ? CloudDissolveBand : 0f;
                if (occludeBelowDepth is { } occludeDepth && transitDepth < occludeDepth - dissolveBand)
                    continue;

                if (transitDepth > 0f && TransitFade(transitDepth - overheadViewDepth) <= 0.01f)
                    continue;

                _zPasses.Add((transitUid, transitDepth, false, true));
            }
        }

        // Painter's algorithm.
        _zPasses.Sort((a, b) =>
        {
            var order = a.Depth.CompareTo(b.Depth);
            return order != 0 ? order : (a.MapUid == playerMap).CompareTo(b.MapUid == playerMap);
        });

        foreach (var pass in _zPasses)
        {
            lowestDepth = Math.Min(lowestDepth, pass.Depth);
            highestDepth = Math.Max(highestDepth, pass.Depth);
        }

        ZEye? MakeZEye(EntityUid targetMap, float d)
        {
            if (_fallbackEye is null || !_mapQuery.Value.TryComp(targetMap, out var mapComp))
                return null;

            Angle rot = _fallbackEye.Rotation * -1;
            var (scale, heightOffset) = ZProjection(d, ownDepth);
            var off = rot.ToWorldVec() * heightOffset;

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
        void PrepareBeams(int splitDepth, IClydeViewport target)
        {
            if (!_entityManager.TryGetComponent<CEZMapComponent>(riderTransit?.LowerMap ?? playerMap, out var observerLevel)
                || !_overlayManager.TryGetOverlay<CEShieldBeamOverlayDispatcher>(out var beams))
                return;
            (int, Vector2, Vector2)? ProjectBeamPoint(EntityUid pointMap, Vector2 position)
            {
                if (!_entityManager.TryGetComponent<CEZMapComponent>(pointMap, out var level)
                    || level.NetworkUid != observerLevel.NetworkUid)
                    return null;
                var depth = level.Depth - observerLevel.Depth - frac;
                Vector2 ProjectReference(float referenceDepth, Vector2 worldPosition)
                {
                    var eye = MakeZEye(pointMap, referenceDepth)!;
                    eye.GetViewMatrix(out var matrix, viewport.RenderScale);
                    var local = Vector2.Transform(worldPosition, matrix) * EyeManager.PixelsPerMeter;
                    return (Vector2) viewport.Size / 2 + new Vector2(local.X, -local.Y);
                }
                var point = ProjectReference(depth, position);
                return (level.Depth, point, ProjectReference(depth + 1, position) - point);
            }

            float BeamCloudCoverage(EntityUid map)
            {
                if (!_entityManager.TryGetComponent<CEZMapComponent>(map, out var level))
                    return 0f;
                var depth = level.Depth - observerLevel.Depth - frac;
                return depth <= CloudFullCoverDepth ? 1f : CloudCoverage(depth);
            }

            beams.PreparePass(target, ProjectBeamPoint, BeamCloudCoverage,
                _fallbackEye!.Scale.Y * viewport.RenderScale.Y * EyeManager.PixelsPerMeter, splitDepth);
        }

        var beamTopDepth = _zPasses
            .Where(pass => !pass.Transit && _entityManager.HasComponent<CEZMapComponent>(pass.MapUid))
            .Select(pass => (int?) _entityManager.GetComponent<CEZMapComponent>(pass.MapUid).Depth)
            .Max();
        _overlayManager.TryGetOverlay<ShipShieldShellOverlay>(out var shipShields); // Mono
        var drewOverheadCloud = false;
        void DrawOverheadCloud()
        {
            drewOverheadCloud = true;
            if (aboveMap is not { } cloudMap ||
                !_entityManager.TryGetComponent(cloudMap, out CEZCloudLayerComponent? cloudAbove))
                return;

            var coverage = CloudCoverage(aboveDepth);
            if (coverage > 0.001f)
                DrawClouds(renderHandle, viewport, MakeZEye(cloudMap, aboveDepth),
                    GetLitCloudColor(cloudMap, cloudAbove.CloudColor), coverage);
        }

        foreach (var (mapUid, depth, allowFov, isTransit) in _zPasses)
        {
            // These ships are composited over the cloud backdrop instead of rendered twice.
            if (isTransit && backdropCloudMap != null && occludeBelowDepth is { } cloudDepth && depth <= cloudDepth)
                continue;

            if (!drewOverheadCloud && mapUid != playerMap && depth >= aboveDepth)
                DrawOverheadCloud();

            using var shieldPass = shipShields?.BeginZPass(_zPasses, mapUid, playerMap,
                viewport, _fallbackEye!, observerAltitude, ownDepth); // Mono
            // Draw the opaque cloud deck beneath this pass.
            CEZCloudLayerComponent? cloudDeck = null;
            if (depth <= 0.001f && !isTransit)
                _entityManager.TryGetComponent(mapUid, out cloudDeck);

            if (riderDeck == null && first && cloudDeck == null && mapUid == playerMap &&
                depth == 0f)
            {
                viewport.Eye = _fallbackEye;
            }
            else
            {
                if (MakeZEye(mapUid, depth) is not { } zEye)
                    continue;
                // Mono Start: Cache the eye that actually draws the aimed-at deck.
                if (zLevelViewer.LookUp && !isTransit && mapUid == aboveMap)
                {
                    _lookUpAimEye = zEye;
                    _lookUpSourceMap = _fallbackEye.Position.MapId;
                }
                // Mono End
                zEye.Primary = mapUid == playerMap && !isTransit;
                zEye.DrawFov = _fallbackEye.DrawFov && allowFov;
                zEye.DrawParallax = riderDeck == null && !isTransit && depth == lowestDepth && cloudDeck == null;

                if (isTransit && depth > 0f)
                {
                    var effectDepth = depth - (_entityManager.HasComponent<CEZTransitMapComponent>(mapUid) ? overheadViewDepth : 0f);
                    RenderTransitOverhead(renderHandle, viewport, mapUid, zEye, effectDepth, shipShields);
                    continue;
                }

                viewport.Eye = zEye;
            }


            if (riderDeck != null && mapUid == playerMap && !isTransit && depth == ownDepth)
            {
                var cloudColor = GetLitCloudColor(aboveMap!.Value, riderDeck.CloudColor);
                // Procedural clouds need the plane's projection, not its map render target.
                var cloudEye = MakeZEye(playerMap, aboveDepth);
                DrawClouds(renderHandle, viewport, cloudEye, cloudColor, 1f, clear: first);
                DrawClouds(renderHandle, viewport, cloudEye, cloudColor, 0f, 0.85f);
                first = false;
            }


            else if (cloudDeck != null)
            {
                var cloudColor = GetLitCloudColor(mapUid, cloudDeck.CloudColor);
                DrawClouds(renderHandle, viewport, viewport.Eye, cloudColor, 1f, clear: first);
                var wisp = 0.85f * Math.Clamp(1f + depth * 2f, 0f, 1f);
                DrawClouds(renderHandle, viewport, viewport.Eye, cloudColor, 0f, wisp);

                if (mapUid == backdropCloudMap)
                {
                    foreach (var (sinkMap, sinkDepth, _, sinkTransit) in _zPasses)
                    {
                        var below = depth - sinkDepth;
                        if (!sinkTransit || below < 0f || below >= CloudDissolveBand ||
                            MakeZEye(sinkMap, sinkDepth) is not { } sinkEye)
                            continue;

                        var t = below / CloudDissolveBand;
                        var fog = t * t * (3f - 2f * t);
                        using var sinkShieldPass = shipShields?.BeginZPass(_zPasses, sinkMap, playerMap,
                            GetTransitViewport(viewport), _fallbackEye!, observerAltitude, ownDepth);
                        BlitTransitCloudGhost(renderHandle, viewport, sinkEye, cloudColor, tint: fog * 0.75f, alpha: 1f - fog);
                    }
                }
                first = false;
            }

            viewport.ClearColor = first ? Color.Black : null;
            // Keep the same render path when a deck crosses zero visual depth.
            var isolate = !first;
            first = false;
            var target = isolate ? GetTransitViewport(viewport) : viewport;
            using var isolatedShieldPass = isolate
                ? shipShields?.BeginZPass(_zPasses, mapUid, playerMap, target, _fallbackEye!, observerAltitude, ownDepth)
                : null;
            if (isolate)
            {
                target.Eye = viewport.Eye;
                target.ClearColor = new Color(0f, 0f, 0f, 0f);
            }

            if (!isTransit && _entityManager.TryGetComponent<CEZMapComponent>(mapUid, out var beamLevel)
                && beamLevel.Depth == beamTopDepth)
                PrepareBeams(beamLevel.Depth, target);
            target.Render();
            if (isolate)
                CompositeZDeck(renderHandle, viewport, target.RenderTarget.Texture);

        }

        var drewPlanetTransit = false;
        var planetQuery = _entityManager.EntityQueryEnumerator<PlanetTransitMapComponent>();
        while (planetQuery.MoveNext(out var transitMap, out var mapTransit))
        {
            if (mapTransit.OriginMap != playerMap)
                continue;

            var duration = (mapTransit.End - mapTransit.Start).TotalSeconds;
            var progress = duration <= 0
                ? 1f
                : Math.Clamp((float)((_gameTiming.CurTime - mapTransit.Start).TotalSeconds / duration), 0f, 1f);
            var depthProgress = progress * progress * (3f - 2f * progress);
            var depth = mapTransit.Arrival
                ? 1f - depthProgress + 0.001f
                : mapTransit.Direction == PlanetTransitDirection.Ascent
                    ? depthProgress + 0.001f
                    : -depthProgress - 0.001f;
            var fade = mapTransit.Arrival
                ? depthProgress
                : Math.Clamp((1f - progress) / 0.5f, 0f, 1f);
            if (fade <= 0.001f || MakeZEye(transitMap, depth) is not { } transitEye)
                continue;

            if (mapTransit.Direction == PlanetTransitDirection.Ascent && !mapTransit.Arrival)
                transitEye.Offset = _fallbackEye.Offset - (transitEye.Offset - _fallbackEye.Offset);

            var shrinkProgress = mapTransit.Arrival ? 1f - progress : progress;
            var shrink = MathF.Pow(0.01f / CESharedZLevelsSystem.ZLevelViewShrink,
                shrinkProgress * shrinkProgress);
            BlitTransitCloudGhost(renderHandle,
                viewport,
                transitEye,
                Color.Black,
                0f,
                fade,
                shrink);
            drewPlanetTransit = true;
        }

        if (drewPlanetTransit && _fallbackEye != null)
        {
            var ownEye = new ZEye(lowestDepth, ownDepth, highestDepth)
            {
                Position = _fallbackEye.Position,
                DrawFov = _fallbackEye.DrawFov,
                DrawLight = _fallbackEye.DrawLight,
                DrawParallax = false,
                Offset = _fallbackEye.Offset,
                Rotation = _fallbackEye.Rotation,
                Scale = _fallbackEye.Scale,
            };
            BlitTransitCloudGhost(renderHandle, viewport, ownEye, Color.Black, 0f, 1f);
        }

        if (!drewOverheadCloud)
            DrawOverheadCloud();

        Eye = _fallbackEye;
        viewport.Eye = Eye;
    }

    private void RenderFallback(IClydeViewport viewport)
    {
        viewport.Eye = _fallbackEye;
        viewport.ClearColor = Color.Black;
        viewport.Render();
    }

    private void RenderPlanetTransitRider(IRenderHandle renderHandle,
        IClydeViewport viewport,
        EntityUid transitMap,
        PlanetTransitMapComponent transit)
    {
        if (_fallbackEye == null ||
            !_mapQuery!.Value.TryComp(transit.OriginMap, out var originMap) ||
            !_mapQuery.Value.TryComp(transitMap, out var transitMapComp))
        {
            RenderFallback(viewport);
            return;
        }

        var duration = (transit.End - transit.Start).TotalSeconds;
        var progress = duration <= 0
            ? 1f
            : Math.Clamp((float)((_gameTiming.CurTime - transit.Start).TotalSeconds / duration), 0f, 1f);
        progress = progress * progress * (3f - 2f * progress);

        var backdropScale = transit.Direction == PlanetTransitDirection.Ascent
            ? transit.Arrival
                ? MathHelper.Lerp(0.08f, 1f, progress)
                : MathHelper.Lerp(1f, 0.08f, progress)
            : MathHelper.Lerp(1f, 1f / 0.35f, progress);

        ZEye BackdropEye(MapComponent map, float scale, bool parallax, float depth)
        {
            var projection = ZProjection(depth);
            return new ZEye(0f, 0f, 0f)
            {
                PlanetTransitBackdrop = true,
                Position = new MapCoordinates(_fallbackEye.Position.Position, map.MapId),
                DrawFov = false,
                DrawLight = _fallbackEye.DrawLight,
                DrawParallax = parallax,
                Offset = _fallbackEye.Offset + (-_fallbackEye.Rotation).ToWorldVec() * projection.Offset,
                Rotation = _fallbackEye.Rotation,
                Scale = _fallbackEye.Scale * scale * projection.Scale,
            };
        }

        void RenderBackdrop(EntityUid mapUid, MapComponent map, float scale, bool clear, bool parallax, float depth = 0f)
        {
            viewport.Eye = BackdropEye(map, scale, parallax, depth);
            viewport.ClearColor = clear ? Color.Black : null;
            viewport.Render();

            if (_entityManager.TryGetComponent<CEZCloudLayerComponent>(mapUid, out var cloud))
                DrawCloudDeck(renderHandle, viewport, GetLitCloudColor(mapUid, cloud.CloudColor), 1f);
        }

        if (transit.Direction == PlanetTransitDirection.Ascent && !transit.Arrival &&
            _zLevels!.TryGetMapNetwork(transit.OriginMap, out var network) &&
            _entityManager.TryGetComponent<CEZMapComponent>(transit.OriginMap, out var originZ))
        {
            var minimumDepth = network.Comp.SortedMin;
            foreach (var mapUid in network.Comp.SortedZLevels)
            {
                if (_entityManager.TryGetComponent<CEZMapComponent>(mapUid, out var zMap) &&
                    zMap.Depth <= originZ.Depth &&
                    (_entityManager.HasComponent<CEZCloudLayerComponent>(mapUid) ||
                     _entityManager.HasComponent<CEZGroundLayerComponent>(mapUid)))
                {
                    minimumDepth = Math.Max(minimumDepth, zMap.Depth);
                }
            }

            var clear = true;
            foreach (var mapUid in network.Comp.SortedZLevels)
            {
                if (!_entityManager.TryGetComponent<CEZMapComponent>(mapUid, out var zMap) ||
                    zMap.Depth < minimumDepth ||
                    zMap.Depth > originZ.Depth ||
                    !_mapQuery.Value.TryComp(mapUid, out var map))
                {
                    continue;
                }

                RenderBackdrop(mapUid, map, backdropScale, clear, clear, zMap.Depth - originZ.Depth);
                clear = false;
            }
        }
        else
        {
            RenderBackdrop(transit.OriginMap, originMap, backdropScale, true, true);
        }

        var gridEye = new ZEye(0f, 0f, 0f)
        {
            Position = new MapCoordinates(_fallbackEye.Position.Position, transitMapComp.MapId),
            DrawFov = _fallbackEye.DrawFov,
            DrawLight = _fallbackEye.DrawLight,
            DrawParallax = false,
            Offset = _fallbackEye.Offset,
            Rotation = _fallbackEye.Rotation,
            Scale = _fallbackEye.Scale,
        };

        BlitTransitCloudGhost(renderHandle, viewport, gridEye, Color.Black, 0f, 1f);
        if (_entityManager.TryGetComponent<CEZMapComponent>(transit.OriginMap, out var beamOrigin)
            && _overlayManager.TryGetOverlay<CEShieldBeamOverlayDispatcher>(out var beams))
        {
            (int, Vector2, Vector2)? ProjectBeamPoint(EntityUid map, Vector2 position)
            {
                if (!_entityManager.TryGetComponent<CEZMapComponent>(map, out var level)
                    || level.NetworkUid != beamOrigin.NetworkUid)
                    return null;
                Vector2 Project(float depth, Vector2 worldPosition)
                {
                    var eye = BackdropEye(originMap, backdropScale, false, depth);
                    eye.GetViewMatrix(out var matrix, viewport.RenderScale);
                    var local = Vector2.Transform(worldPosition, matrix) * EyeManager.PixelsPerMeter;
                    return (Vector2) viewport.Size / 2 + new Vector2(local.X, -local.Y);
                }
                var depth = level.Depth - beamOrigin.Depth;
                var point = Project(depth, position);
                return (level.Depth, point, Project(depth + 1, position) - point);
            }

            float BeamCloudCoverage(EntityUid map) =>
                _entityManager.TryGetComponent<CEZMapComponent>(map, out var level)
                && level.Depth <= beamOrigin.Depth ? 1f : 0f;

            beams.RenderPass(renderHandle, viewport, ProjectBeamPoint, BeamCloudCoverage,
                _fallbackEye.Scale.Y * backdropScale * viewport.RenderScale.Y * EyeManager.PixelsPerMeter);
        }
        Eye = _fallbackEye;
        viewport.Eye = Eye;
    }

    private void RenderTransitOverhead(IRenderHandle renderHandle,
        IClydeViewport viewport,
        EntityUid transitMap,
        ZEye zEye,
        float depth,
        ShipShieldShellOverlay? shipShields)
    {
        var scratch = GetTransitViewport(viewport);

        _transitBlitShader ??= _prototypeManager.Index<ShaderPrototype>("CEZBlurBlit").InstanceUnique();

        zEye.DrawParallax = false;

        scratch.Eye = zEye;
        // "Why aren't you using Color.Transparent" because it's LIES it is entirely white
        scratch.ClearColor = new Color(0f, 0f, 0f, 0f);
        scratch.Render();

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
        var fade = TransitFade(depth);
        var isShipTransit = _entityManager.TryGetComponent(transitMap, out CEZTransitMapComponent? transit);
        if (transit != null)
        {
            var span = _zLevels!.GetTransitSpan(transit);
            var progress = GetTransitProgress(transit) * span;

            if (transit.UpperMap is { } upper &&
                _entityManager.TryGetComponent(upper, out CEZCloudLayerComponent? cloudAbove))
            {
                cloud = CloudCoverage(span - progress);
                var color = GetLitCloudColor(upper, cloudAbove.CloudColor);
                cloudColor = new Vector3(color.R, color.G, color.B);
            }

            if (transit.LowerMap is { } lower &&
                _entityManager.TryGetComponent(lower, out CEZCloudLayerComponent? cloudBelow))
            {
                var belowCover = CloudCoverage(progress);
                if (belowCover > cloud)
                {
                    cloud = belowCover;
                    var color = GetLitCloudColor(lower, cloudBelow.CloudColor);
                    cloudColor = new Vector3(color.R, color.G, color.B);
                }
            }
        }

        if (isShipTransit)
            shipShields?.DrawTransitSurface(renderHandle, viewport, scratch, fade, aboveHull: false);

        var screenHandle = renderHandle.DrawingHandleScreen;
        screenHandle.RenderInRenderTarget(viewport.RenderTarget, () =>
        {
            var texture = scratch.RenderTarget.Texture;

            _transitBlitShader.SetParameter("BLUR_COLOR", hazeColor);
            _transitBlitShader.SetParameter("STRENGTH", strength);
            _transitBlitShader.SetParameter("CLOUD_COLOR", cloudColor);
            _transitBlitShader.SetParameter("CLOUD", cloud);
            _transitBlitShader.SetParameter("FADE", fade);

            screenHandle.UseShader(_transitBlitShader);
            screenHandle.DrawTextureRect(texture, new UIBox2(Vector2.Zero, texture.Size));
            screenHandle.UseShader(null);
        }, null);
        shipShields?.DrawTransitSurface(renderHandle, viewport, scratch, fade, aboveHull: isShipTransit ? true : null);
    }

    private IClydeViewport GetTransitViewport(IClydeViewport viewport)
    {
        if (_transitViewport == null || _transitViewport.Size != viewport.Size)
        {
            _transitViewport?.Dispose();
            _transitViewport = _clyde.CreateViewport(viewport.Size, nameof(_transitViewport));
        }

        _transitViewport.RenderScale = viewport.RenderScale;
        return _transitViewport;
    }

    private void CompositeZDeck(IRenderHandle render, IClydeViewport viewport, Texture texture)
    {
        _zDeckComposite ??= _prototypeManager.Index<ShaderPrototype>("CEZDeckComposite").Instance();
        var handle = render.DrawingHandleScreen;
        handle.RenderInRenderTarget(viewport.RenderTarget, () =>
        {
            handle.UseShader(_zDeckComposite);
            handle.DrawTextureRect(texture, new UIBox2(Vector2.Zero, texture.Size));
            handle.UseShader(null);
        }, null);
    }

    /// <summary>Composites a transit hull over the cloud deck with tint and fade.</summary>
    private void BlitTransitCloudGhost(IRenderHandle renderHandle,
        IClydeViewport viewport,
        IEye? eye,
        Color cloudColor,
        float tint,
        float alpha,
        float shrink = 1f)
    {
        if (eye is null || alpha <= 0.001f)
            return;

        var scratch = GetTransitViewport(viewport);

        _transitBlitShader ??= _prototypeManager.Index<ShaderPrototype>("CEZBlurBlit").InstanceUnique();

        const float minEyeShrink = 0.25f;
        var eyeShrink = Math.Max(shrink, minEyeShrink);
        var textureShrink = shrink / eyeShrink;
        var eyeScale = eye.Scale;
        eye.Scale *= eyeShrink;
        scratch.Eye = eye;
        scratch.ClearColor = new Color(0f, 0f, 0f, 0f);
        scratch.Render();
        eye.Scale = eyeScale;

        var col = new Vector3(cloudColor.R, cloudColor.G, cloudColor.B);
        var screenHandle = renderHandle.DrawingHandleScreen;
        screenHandle.RenderInRenderTarget(viewport.RenderTarget, () =>
        {
            var texture = scratch.RenderTarget.Texture;

            _transitBlitShader.SetParameter("BLUR_COLOR", new Vector3(0f, 0f, 0f));
            _transitBlitShader.SetParameter("STRENGTH", 0f);
            _transitBlitShader.SetParameter("CLOUD_COLOR", col);
            _transitBlitShader.SetParameter("CLOUD", Math.Clamp(tint, 0f, 1f));
            _transitBlitShader.SetParameter("FADE", Math.Clamp(alpha, 0f, 1f));

            screenHandle.UseShader(_transitBlitShader);
            var size = (Vector2) texture.Size;
            var half = size / 2f;
            var rect = textureShrink < 1f
                ? new UIBox2(half - half * textureShrink, half + half * textureShrink)
                : new UIBox2(Vector2.Zero, size);
            screenHandle.DrawTextureRect(texture, rect);
            screenHandle.UseShader(null);
        }, null);
    }

    /// <summary>
    /// Transit hulls fade from clear at the cloud plane to hidden one deck below it.
    /// </summary>
    private const float CloudDissolveBand = 1f;

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

    private void DrawClouds(IRenderHandle renderHandle, IClydeViewport viewport, IEye? eye,
        Color color, float coverage, float wisp = 0f, bool clear = false)
    {
        if (eye == null || (coverage <= 0f && wisp <= 0f))
            return;

        _cloudShader ??= _prototypeManager.Index<ShaderPrototype>("CEZClouds").InstanceUnique();

        eye.GetViewMatrixInv(out var inverseView, viewport.RenderScale);
        var size = (Vector2) viewport.Size / EyeManager.PixelsPerMeter;
        var origin = Vector2.Transform(-size / 2f, inverseView);
        var axisX = Vector2.TransformNormal(new Vector2(size.X, 0f), inverseView);
        var axisY = Vector2.TransformNormal(new Vector2(0f, size.Y), inverseView);

        var screenHandle = renderHandle.DrawingHandleScreen;
        screenHandle.RenderInRenderTarget(viewport.RenderTarget, () =>
        {
            _cloudShader.SetParameter("CLOUD_COLOR", new Vector3(color.R, color.G, color.B));
            _cloudShader.SetParameter("COVERAGE", coverage);
            _cloudShader.SetParameter("WISP", wisp);
            _cloudShader.SetParameter("WORLD_ORIGIN", origin);
            _cloudShader.SetParameter("WORLD_X", axisX);
            _cloudShader.SetParameter("WORLD_Y", axisY);

            screenHandle.UseShader(_cloudShader);
            screenHandle.DrawRect(new UIBox2(Vector2.Zero, viewport.RenderTarget.Texture.Size), Color.White);
            screenHandle.UseShader(null);
        }, clear ? Color.Black : null);
    }

    private Color GetLitCloudColor(EntityUid mapUid, Color color)
    {
        if (!_entityManager.TryGetComponent<MapLightComponent>(mapUid, out var light))
            return color;

        var ambient = _entityManager.System<Content.Client.Light.LightCycleSystem>().SampleAmbient(mapUid, light);
        return new Color(color.R * ambient.R, color.G * ambient.G, color.B * ambient.B, color.A);
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

        /// <summary>Parallax is restricted to the bottom pass.</summary>
        public bool DrawParallax = true;

        /// <summary>Marks the player's own deck pass for screen-space overlays.</summary>
        public bool Primary;

        public bool PlanetTransitBackdrop;
    }
}
