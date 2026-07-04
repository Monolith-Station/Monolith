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
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly ITileDefinitionManager _tile = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

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

        if (!_mapManager.TryFindGridAt(mapUid, mapCoordsBottomLeft.Position, out var gridUid, out var grid))
            return true;

        _mapSystem ??= _entityManager.System<SharedMapSystem>();

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

    /// <summary>
    /// Secondary viewport for grids transiting ABOVE the observer: rendering them to a
    /// transparent target lets the composite apply haze and fade to just the ship,
    /// instead of fogging the whole already-drawn scene.
    /// </summary>
    private IClydeViewport? _transitViewport;
    private ShaderInstance? _transitBlitShader;

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

        // When riding a grid between two levels, the world below starts a fractional
        // depth away instead of a whole one, and slides continuously as the grid moves.
        var frac = 0f;
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
            if (_zLevels.TryMapOffset(playerMap, -1, out var mapBelow))
                belowChainStart = mapBelow.Value;
            if (_zLevels.TryMapUp(playerMap, out var mapAbove))
                aboveMap = mapAbove.Value;
        }

        // Walk downward while there are empty tiles to see through.
        if (TryFindEmptyTiles(playerMap))
        {
            var current = belowChainStart;
            var depthCursor = belowChainStartDepth;
            for (var i = 0; i < CESharedZLevelsSystem.MaxZLevelsBelowRendering && current != null; i++)
            {
                _zPasses.Add((current.Value, depthCursor, false, false));

                if (!TryFindEmptyTiles(current.Value))
                    break;

                current = _zLevels.TryMapOffset(current.Value, -1, out var next) ? next.Value.Owner : null;
                depthCursor -= 1f;
            }
        }

        // The player's own map always renders, at depth 0 with the real eye.
        _zPasses.Add((playerMap, 0f, true, false));

        if (riderTransit != null)
        {
            if (aboveMap != null && aboveDepth > 0.001f && TransitFade(aboveDepth) > 0.01f)
                _zPasses.Add((aboveMap.Value, aboveDepth, false, true));
        }
        else if (zLevelViewer.LookUp && aboveMap != null)
        {
            _zPasses.Add((aboveMap.Value, aboveDepth, true, false));
        }

        // Grids in transit render between the levels they're crossing. Selection is by
        // ABSOLUTE altitude difference, so ships in any gap of the observer's network
        // render — not just gaps bordering the maps already in the pass list.
        var altitudeAnchor = riderTransit?.LowerMap ?? playerMap;
        if (_entityManager.TryGetComponent(altitudeAnchor, out CEZLevelMapComponent? anchorZ))
        {
            var observerAltitude = anchorZ.Depth + frac;
            _zLevels.TryZNetwork(altitudeAnchor, out var observerNetwork);

            var transitQuery = _entityManager.EntityQueryEnumerator<CEZTransitMapComponent>();
            while (transitQuery.MoveNext(out var transitUid, out var transit))
            {
                if (transitUid == playerMap || transit.LowerMap is not { } lowerMap)
                    continue;

                if (!_entityManager.TryGetComponent(lowerMap, out CEZLevelMapComponent? lowerZ))
                    continue;

                // Other z-networks are other worlds.
                if (observerNetwork != null &&
                    (!_zLevels.TryZNetwork(lowerMap, out var transitNetwork) ||
                     transitNetwork.Value.Owner != observerNetwork.Value.Owner))
                {
                    continue;
                }

                var transitDepth = lowerZ.Depth + GetTransitProgress(transit) - observerAltitude;

                // Fully dissolved into the sky: not worth a render pass.
                if (transitDepth > 0f && TransitFade(transitDepth) <= 0.01f)
                    continue;

                // No FOV on these: a ship overhead is in open sky, not behind your walls.
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
            if (mapUid == playerMap && depth == 0f)
            {
                viewport.Eye = _fallbackEye;
            }
            else
            {
                if (!_mapQuery.Value.TryComp(mapUid, out var mapComp))
                    continue;

                Angle rotation = _fallbackEye.Rotation * -1;
                var offset = rotation.ToWorldVec() * CEClientZLevelsSystem.ZLevelOffset * depth;

                // Perspective: each level away from the observer's plane is drawn a
                // constant factor smaller below and larger above, continuously with
                // depth. Symmetric through depth 0, so a ship rising overhead grows
                // by the same curve it shrinks by when it sinks — no pop when it
                // crosses your plane and reverses direction.
                var scale = _fallbackEye.Scale * MathF.Pow(CESharedZLevelsSystem.ZLevelViewShrink, -depth);

                var zEye = new ZEye(lowestDepth, depth, highestDepth)
                {
                    Position = new MapCoordinates(_fallbackEye.Position.Position, mapComp.MapId),
                    DrawFov = _fallbackEye.DrawFov && allowFov && depth >= 0,
                    DrawLight = _fallbackEye.DrawLight,
                    DrawParallax = !isTransit && depth == lowestDepth,
                    Offset = _fallbackEye.Offset + offset,
                    Rotation = _fallbackEye.Rotation,
                    Scale = scale,
                };

                // Ships overhead get their own transparent pass composited with
                // height-scaled haze and fade, so the fog touches only the ship.
                if (isTransit && depth > 0f)
                {
                    RenderTransitOverhead(renderHandle, viewport, mapUid, zEye, depth);
                    continue;
                }

                viewport.Eye = zEye;
            }

            viewport.ClearColor = first ? Color.Black : null;
            first = false;
            viewport.Render();
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
        // NOT Color.Transparent: that's WHITE with zero alpha, and any blend or blur
        // leakage turns it into a white wash. Transparent black misbehaves invisibly.
        _transitViewport.ClearColor = new Color(0f, 0f, 0f, 0f);
        _transitViewport.Render();

        // The transit map's own MapLight is already altitude-lerped, making it the
        // correct haze tint for the ship at its current height.
        var hazeColor = new Vector3(0, 0, 1);
        if (_entityManager.TryGetComponent(transitMap, out MapLightComponent? mapLight))
        {
            hazeColor = new Vector3(
                mapLight.AmbientLightColor.R,
                mapLight.AmbientLightColor.G,
                mapLight.AmbientLightColor.B);
        }

        var strength = Math.Clamp(depth, 0f, 1f);

        var screenHandle = renderHandle.DrawingHandleScreen;
        screenHandle.RenderInRenderTarget(viewport.RenderTarget, () =>
        {
            var texture = _transitViewport.RenderTarget.Texture;

            _transitBlitShader.SetParameter("BLUR_COLOR", hazeColor);
            _transitBlitShader.SetParameter("STRENGTH", strength);
            // Ships dissolve into the sky as they climb away from the observer and
            // materialize out of it on the way down.
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
        /// Whether the parallax may draw on this pass. Only the deepest LEVEL pass
        /// wants it; transit passes (mostly-empty maps holding a moving ship) must
        /// never paint the skybox over the already-rendered world.
        /// </summary>
        public bool DrawParallax = true;
    }
}
