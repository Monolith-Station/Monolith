using System.Numerics;
using Content.Client._CE.ZLevels.Core.Overlays;
using Content.Client.Viewport;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Mono.Ships;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Map.Components;
using static Robust.Client.Graphics.DrawingHandleBase;

namespace Content.Client._Mono.ShipShieldShell;

internal sealed partial class ShipShieldShellOverlay
{
    private const int Segments = 48;
    private const int Bands = 20;
    private static readonly Vector2[] Directions = BuildDirections();
    private readonly DrawVertexUV2DColor[] _lowerRing = new DrawVertexUV2DColor[Segments];
    private readonly DrawVertexUV2DColor[] _upperRing = new DrawVertexUV2DColor[Segments];
    private readonly DrawVertexUV2DColor[] _surfaceTriangles = new DrawVertexUV2DColor[Segments * Bands * 12];
    private readonly Vector2[] _terrainTileVertices = new Vector2[4];
    private ZPass? _zPass;

    internal ZPassScope BeginZPass(IReadOnlyList<(EntityUid MapUid, float Depth, bool AllowFov, bool Transit)> planes,
        EntityUid map, EntityUid observerMap, IClydeViewport viewport, IEye baseEye, float visualAltitude, float anchor)
    {
        var scope = new ZPassScope(this, _zPass);
        _zPass = new ZPass(planes, map, observerMap, viewport, baseEye, visualAltitude, anchor);
        return scope;
    }

    private bool TryGetSurfaceRange(EntityUid? emitterMap, Vector2 center, Vector3 radii, float centerAltitude,
        out float bottom, out float top, bool observerDeckOnly = false)
    {
        bottom = -1f;
        top = 1f;
        var pass = _zPass!.Value;
        var map = observerDeckOnly ? pass.ObserverMap : pass.Map;
        // A moving shell stays on its own hull's pass.
        if (entities.HasComponent<CEZTransitMapComponent>(emitterMap))
            return map == emitterMap;

        var altitude = _zLevels.GetAbsoluteAltitude(map);
        var stack = _shells.GetStack(map);
        var emitterVisible = false;
        var currentVisible = false;
        var hasPriority = true;
        var diameter = 2f * MathF.Max(radii.X, radii.Y);
        var shellBounds = Box2.CenteredAround(center, new Vector2(diameter));
        foreach (var plane in pass.Planes)
        {
            var other = plane.MapUid;
            if (_shells.GetStack(other) != stack)
                continue;

            var otherAltitude = _zLevels.GetAbsoluteAltitude(other);
            if (entities.TryGetComponent(other, out CEZTransitMapComponent? transit))
            {
                if (observerDeckOnly)
                    continue;

                var height = MathF.Abs(otherAltitude - centerAltitude);
                var intersects = height < radii.Z &&
                    entities.TryGetComponent(transit.PrimaryGrid, out MapGridComponent? grid) &&
                    _transform.GetWorldMatrix(grid.Owner).TransformBox(grid.LocalAABB).Intersects(shellBounds);
                if (!intersects && other != pass.ObserverMap)
                    continue;
            }
            else if (other != emitterMap && plane.Transit && plane.Depth > 0f)
            {
                // The rider's fading preview of a fixed deck is not a hull plane.
                continue;
            }

            emitterVisible |= other == emitterMap;
            if (other == map)
            {
                currentVisible = true;
                continue;
            }

            if (otherAltitude > altitude)
                top = MathF.Min(top, (otherAltitude - centerAltitude) / radii.Z);
            else if (otherAltitude < altitude)
                bottom = MathF.Max(bottom, (altitude - centerAltitude) / radii.Z);
            else if (Priority(other) > Priority(map) || Priority(other) == Priority(map) && other.Id < map.Id)
                hasPriority = false;
        }

        if (!currentVisible || !hasPriority)
            return false;

        if (!emitterVisible && centerAltitude < altitude)
            bottom = MathF.Max(bottom, (altitude - centerAltitude) / radii.Z);

        return bottom < top;

        int Priority(EntityUid map) => map == emitterMap ? 2 : map == pass.ObserverMap ? 1 : 0;
    }

    private void ClipAgainstTerrain(EntityUid network, Vector2 center, Angle rotation, Vector3 radii,
        float centerAltitude, float emitterAltitude, ref float bottom, ref float top)
    {
        if (!entities.TryGetComponent(network, out CEZMapNetworkComponent? levels))
            return;

        foreach (var map in levels.SortedZLevels)
        {
            if (!entities.TryGetComponent(map, out MapGridComponent? grid))
                continue;

            var altitude = _zLevels.GetAbsoluteAltitude(map);
            var z = (altitude - centerAltitude) / radii.Z;
            if (MathF.Abs(z) >= 1f || (altitude <= emitterAltitude ? z <= bottom : z >= top))
                continue;

            if (!CoversShieldSlice(grid, center, rotation, new Vector2(radii.X, radii.Y) * RingRadius(z)))
                continue;

            if (altitude <= emitterAltitude)
                bottom = z;
            else
                top = z;
        }
    }

    private bool CoversShieldSlice(MapGridComponent grid, Vector2 center, Angle rotation, Vector2 radii)
    {
        var toGrid = Matrix3x2.CreateScale(radii) * Matrix3x2.CreateRotation((float) rotation.Theta) *
            Matrix3x2.CreateTranslation(center) * _transform.GetInvWorldMatrix(grid.Owner);
        if (!Matrix3x2.Invert(toGrid, out var toSlice))
            return false;

        var extent = new Vector2(new Vector2(toGrid.M11, toGrid.M21).Length(),
            new Vector2(toGrid.M12, toGrid.M22).Length());
        var min = (toGrid.Translation - extent) / grid.TileSize;
        var max = (toGrid.Translation + extent) / grid.TileSize;
        for (var y = (int) MathF.Floor(min.Y); y < (int) MathF.Ceiling(max.Y); y++)
        {
            for (var x = (int) MathF.Floor(min.X); x < (int) MathF.Ceiling(max.X); x++)
            {
                var indices = new Vector2i(x, y);
                if (_maps.TryGetTileRef(grid.Owner, grid, indices, out var tile) && !tile.Tile.IsEmpty &&
                    !((ContentTileDefinition) tiles[tile.Tile.TypeId]).Transparent)
                    continue;

                SharedShipShieldShellSystem.WriteGridTileVertices(indices, grid.TileSize, _terrainTileVertices);
                SharedShipShieldShellSystem.GetFootprintExtrema(_terrainTileVertices, toSlice, out var closest, out _);
                if (closest.LengthSquared() < 1f)
                    return false;
            }
        }

        return true;
    }

    private int BuildProjectedShell(IEye eye, EntityUid? emitterMap, Vector2 center, Angle rotation,
        Vector3 radii, float altitude, float bottom, float top, float interior, bool lookingUp, Color color, bool impact,
        bool? aboveHull, bool underside, ShaderInstance? surfaceShader = null, Vector2 spoolOffset = default)
    {
        var pass = _zPass!.Value;
        entities.TryGetComponent(emitterMap, out CEZTransitMapComponent? transitMap);
        // Hull planes cut the mesh for draw order; only the emitter determines its projection.
        var referenceAltitude = transitMap != null
            ? Math.Clamp(_zLevels.GetAbsoluteAltitude(emitterMap!.Value), altitude - radii.Z, altitude + radii.Z)
            : altitude;
        var referenceDepth = referenceAltitude - pass.VisualAltitude;
        var (referenceScale, referenceOffset) = ScalingViewport.ZProjection(referenceDepth, pass.Anchor);
        var baseEye = pass.BaseEye;
        var referenceCenter = baseEye.Position.Position + baseEye.Offset + (-baseEye.Rotation).ToWorldVec() * referenceOffset;
        var scale = baseEye.Scale * referenceScale;
        var referenceView = Matrix3Helpers.CreateInverseTransform(
            referenceCenter.X, referenceCenter.Y, -baseEye.Rotation.Theta, 1f / scale.X, 1f / scale.Y);
        eye.GetViewMatrix(out var passView, Vector2.One);
        if (!Matrix3x2.Invert(passView, out var inversePassView))
            return 0;

        // Use the same screen projection for every slice, then undo the current pass's view.
        var toPass = referenceView * inversePassView;
        var passCenter = Vector2.Transform(center, toPass);
        var xyRadii = new Vector2(radii.X, radii.Y);
        var heightOffset = (-baseEye.Rotation).RotateVec(Vector2.UnitY) *
            (CESharedZLevelsSystem.ZLevelOffset * radii.Z);
        var heightAxis = Vector2.TransformNormal(heightOffset, toPass);
        // The projection's view direction in normalized ellipsoid coordinates.
        var localHeight = (-rotation).RotateVec(heightOffset) / xyRadii;
        var viewDirection = Vector3.Normalize(new Vector3(-localHeight, 1f));
        if (underside)
            viewDirection = -viewDirection;

        if (surfaceShader != null && emitterMap is { } map)
        {
            var emitterDepth = _zLevels.GetAbsoluteAltitude(map) - pass.VisualAltitude;
            var (emitterScale, emitterOffset) = ScalingViewport.ZProjection(emitterDepth, pass.Anchor);
            var emitterEyeCenter = referenceCenter + (-baseEye.Rotation).ToWorldVec() * (emitterOffset - referenceOffset);
            var originOffset = referenceCenter - center + (center - emitterEyeCenter) * (emitterScale / referenceScale);
            var origin = ((-rotation).RotateVec(originOffset) + spoolOffset * (emitterScale / referenceScale)) / xyRadii;
            surfaceShader.SetParameter("MESH_PROJECTION", new Vector4(localHeight.X, localHeight.Y, origin.X, origin.Y));
            surfaceShader.SetParameter("MESH_VIEW", viewDirection);
        }
        var ambientRgb = transitMap != null
            ? Vector3.Lerp(AmbientRgb(transitMap.LowerMap), AmbientRgb(transitMap.UpperMap ?? transitMap.LowerMap),
                _zLevels.GetTransitProgress(transitMap))
            : AmbientRgb(emitterMap);
        var interiorFade = pass.Map == pass.ObserverMap ? interior : 0f;
        // Both sides of a moving shell belong to the same deck. The hull split is only draw order.
        var transitInterior = transitMap != null ? interiorFade : 0f;
        var fadeBottom = bottom;
        var fadeTop = top;
        // A visiting hull can draw part of this deck's shell without owning its interior fade.
        if (interior > 0f && transitMap == null && entities.HasComponent<CEZMapComponent>(pass.ObserverMap) &&
            TryGetSurfaceRange(emitterMap, center, radii, altitude, out var deckBottom, out var deckTop,
                observerDeckOnly: true))
        {
            interiorFade = interior;
            fadeBottom = deckBottom;
            fadeTop = deckTop;
        }

        var fadeWidth = MathF.Min(0.35f / radii.Z, (fadeTop - fadeBottom) / 3f);
        var hullHeight = aboveHull != null ? (_zLevels.GetAbsoluteAltitude(pass.Map) - altitude) / radii.Z : 0f;
        var drawBottom = aboveHull == true ? MathF.Max(bottom, hullHeight) : bottom;
        var drawTop = aboveHull == false ? MathF.Min(top, hullHeight) : top;
        var count = 0;
        for (var band = 0; band < Bands; band++)
        {
            var bandLow = BandHeight(band);
            var bandHigh = BandHeight(band + 1);
            var low = MathF.Max(drawBottom, bandLow);
            var high = MathF.Min(drawTop, bandHigh);
            if (low >= high)
                continue;

            var lowRadius = RingRadius(bandLow);
            var highRadius = RingRadius(bandHigh);
            var from = (low - bandLow) / (bandHigh - bandLow);
            var to = (high - bandLow) / (bandHigh - bandLow);
            var lowStyle = RingStyle(low);
            var highStyle = RingStyle(high);
            for (var i = 0; i < Segments; i++)
            {
                _lowerRing[i] = Vertex(Directions[i] * MathHelper.Lerp(lowRadius, highRadius, from), low, lowStyle);
                _upperRing[i] = Vertex(Directions[i] * MathHelper.Lerp(lowRadius, highRadius, to), high, highStyle);
            }

            for (var segment = 0; segment < Segments; segment++)
            {
                var next = (segment + 1) % Segments;
                if (from == 0f && to == 1f)
                {
                    AddFace(_lowerRing[segment], _lowerRing[next], _upperRing[segment]);
                    AddFace(_lowerRing[next], _upperRing[next], _upperRing[segment]);
                    continue;
                }

                // Preserve the original triangle diagonal when cutting a band in two.
                var a = Directions[next] * lowRadius;
                var b = Directions[segment] * highRadius;
                var lowCut = from == 0f ? _lowerRing[next] : Vertex(Vector2.Lerp(a, b, from), low, lowStyle);
                var highCut = to == 1f ? _upperRing[segment] : Vertex(Vector2.Lerp(a, b, to), high, highStyle);
                AddFace(_lowerRing[segment], lowCut, _upperRing[segment]);
                AddFace(lowCut, highCut, _upperRing[segment]);
                AddFace(lowCut, _lowerRing[next], highCut);
                AddFace(_lowerRing[next], _upperRing[next], highCut);
            }
        }

        return count;

        (Color Color, float Blur, float InteriorFade) RingStyle(float z)
        {
            var blur = MathF.Max(0f, pass.VisualAltitude - (altitude + z * radii.Z));
            blur = MathHelper.Lerp(blur, MathF.Max(0f, pass.VisualAltitude - altitude - hullHeight * radii.Z),
                transitInterior);
            var tint = CEZLevelBlurOverlay.DepthTint(ambientRgb, blur);
            var fade = interiorFade;
            // Keep the adjacent deck untouched and match its opacity exactly at the cut.
            if (fadeBottom > -1f)
                fade *= SmoothFade(0f, fadeWidth, z - fadeBottom);
            if (fadeTop < 1f)
                fade *= SmoothFade(0f, fadeWidth, fadeTop - z);
            return (new Color(color.R * tint.X, color.G * tint.Y, color.B * tint.Z, color.A), blur, fade);
        }

        DrawVertexUV2DColor Vertex(Vector2 local, float z, (Color Color, float Blur, float InteriorFade) style)
        {
            var facing = Vector3.Dot(new Vector3(local, z), viewDirection);
            var side = 1f - SmoothFade(0.04f, 0.45f, facing);
            var insideOpacity = lookingUp ? 0.28f + 0.47f * side : 0.65f * side;
            var opacity = MathHelper.Lerp(1f, insideOpacity, style.InteriorFade);
            if (impact)
                opacity = MathF.Max(opacity, 0.35f);
            var offset = rotation.RotateVec(local * xyRadii);
            var position = passCenter + Vector2.TransformNormal(offset, toPass) + heightAxis * z;
            return new DrawVertexUV2DColor(position, (local + Vector2.One) * 0.5f,
                style.Color.WithAlpha(style.Color.A * opacity))
            {
                UV2 = impact ? new Vector2(1f, z) : new Vector2(4096f + style.Blur, z),
            };
        }

        void AddFace(DrawVertexUV2DColor va, DrawVertexUV2DColor vb, DrawVertexUV2DColor vc)
        {
            var ab = vb.Position - va.Position;
            var ac = vc.Position - va.Position;
            var winding = ab.X * ac.Y - ab.Y * ac.X;
            if (underside ? winding >= 0f : winding <= 0f)
                return;

            _surfaceTriangles[count++] = va;
            _surfaceTriangles[count++] = underside ? vc : vb;
            _surfaceTriangles[count++] = underside ? vb : vc;
        }
    }

    private static float BandHeight(int band) => MathF.Sin(MathF.PI * ((float) band / Bands - 0.5f));

    private Vector3 AmbientRgb(EntityUid? map)
    {
        var light = entities.TryGetComponent(map, out MapLightComponent? component) ? component.AmbientLightColor : Color.Blue;
        return new Vector3(light.R, light.G, light.B);
    }

    private static float RingRadius(float z) => MathF.Sqrt(MathF.Max(0f, 1f - z * z));

    private static Vector2[] BuildDirections()
    {
        var directions = new Vector2[Segments];
        for (var segment = 0; segment < Segments; segment++)
        {
            var (y, x) = MathF.SinCos(MathF.Tau * segment / Segments);
            directions[segment] = new Vector2(x, y);
        }
        return directions;
    }

    internal readonly record struct ZPass(IReadOnlyList<(EntityUid MapUid, float Depth, bool AllowFov, bool Transit)> Planes,
        EntityUid Map, EntityUid ObserverMap, IClydeViewport Viewport, IEye BaseEye, float VisualAltitude, float Anchor);

    internal readonly struct ZPassScope(ShipShieldShellOverlay overlay, ZPass? previous) : IDisposable
    {
        public void Dispose() => overlay._zPass = previous;
    }
}
