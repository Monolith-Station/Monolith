using System.Numerics;
using System.Threading.Tasks;
using Content.Client._Mono.Radar;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Mono.Detection;
using Content.Shared._Mono.PlanetMap;
using Content.Shared._Mono.Planets;
using Content.Shared._Mono.Radar;
using Content.Shared.Parallax.Biomes;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.Shuttles.UI;

public partial class BaseShuttleControl
{
    private static readonly Vector2[] MarkerShape = [new(0f, -4f), new(4f, 0f), new(0f, 4f), new(-4f, 0f)];
    private readonly Vector2[] _markerVerts = new Vector2[MarkerShape.Length + 1];

    private OwnedTexture? _terrainTexture;
    private Rgba32[] _terrainPixels = Array.Empty<Rgba32>();
    private Task? _terrainJob;
    private int _terrainRevision;
    private Vector2i _terrainSize;
    private Matrix3x2 _terrainView;
    private EntityUid? _terrainMap;

    protected override void ExitedTree()
    {
        _terrainTexture?.Dispose();
        _terrainTexture = null;
        _terrainMap = null;
        base.ExitedTree();
    }

    protected Entity<MapGridComponent>? FindTerrainSurface(EntityUid? map, EntityUid? observer)
    {
        if (observer == null)
            return null;

        var detection = EntManager.System<DetectionSystem>();
        var levels = EntManager.System<CESharedZLevelsSystem>();
        var observerAltitude = levels.GetAbsoluteAltitude(observer.Value);
        Entity<MapGridComponent>? surface = null;
        var nearest = float.NegativeInfinity;
        var query = EntManager.AllEntityQueryEnumerator<BiomeComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out _, out var grid))
        {
            if (!detection.SharesRadarSpace(map, uid))
                continue;

            var altitude = levels.GetAbsoluteAltitude(uid) - observerAltitude;
            if (altitude > 0.05f || altitude < nearest)
                continue;

            nearest = altitude;
            surface = (uid, grid);
        }

        return surface;
    }

    protected void DrawTerrainPreview(DrawingHandleScreen handle, Matrix3x2 worldToView, Entity<MapGridComponent> terrain, EntityUid? console)
    {
        if (_terrainJob is { IsCompleted: true })
        {
            _terrainJob.GetAwaiter().GetResult();
            _terrainJob = null;
            if (_terrainTexture == null || _terrainTexture.Size != _terrainSize)
            {
                _terrainTexture?.Dispose();
                _terrainTexture = IoCManager.Resolve<IClyde>().CreateBlankTexture<Rgba32>(_terrainSize, "radar terrain");
            }
            _terrainTexture.SetSubImage<Rgba32>(Vector2i.Zero, _terrainSize, _terrainPixels);
        }

        var size = PixelSize;
        var terrainToView = EntManager.System<SharedTransformSystem>().GetWorldMatrix(terrain.Owner) * worldToView;
        if (console is not { } consoleUid || !Matrix3x2.Invert(terrainToView, out var viewToTerrain))
            return;
        var tileSize = terrain.Comp.TileSize;
        var halfWrap = EntManager.TryGetComponent<ToroidalMapComponent>(terrain.Owner, out var torus)
            ? torus.Size / (2f * tileSize)
            : float.PositiveInfinity;
        var bounds = viewToTerrain.TransformBox(new Box2(Vector2.Zero, size));
        bounds = new Box2(bounds.BottomLeft / tileSize, bounds.TopRight / tileSize);
        const int step = 1;
        var charts = EntManager.System<RadarTerrainSystem>();
        var revision = charts.Request(consoleUid, terrain.Owner, bounds, step);
        var changed = _terrainMap != terrain.Owner || _terrainRevision != revision;
        if (_terrainJob == null && size.X > 0 && size.Y > 0 &&
            (changed || _terrainSize != size || _terrainView != terrainToView))
        {
            _terrainRevision = revision;
            _terrainMap = terrain.Owner;
            _terrainSize = size;
            _terrainView = terrainToView;
            if (_terrainPixels.Length != size.X * size.Y)
                _terrainPixels = new Rgba32[size.X * size.Y];
            var pixels = _terrainPixels;
            var chunks = charts.Snapshot(terrain.Owner);
            void RenderRows(int start, int end)
            {
                for (var y = start; y < end; y++)
                {
                    var previous = new Vector2i(int.MinValue, int.MinValue);
                    var previousChunk = previous;
                    uint[]? data = null;
                    var color = default(Rgba32);
                    for (var x = 0; x < size.X; x++)
                    {
                        var position = Vector2.Transform(new Vector2(x + 0.5f, y + 0.5f), viewToTerrain) / tileSize;
                        if (position.X < -halfWrap || position.X >= halfWrap ||
                            position.Y < -halfWrap || position.Y >= halfWrap)
                        {
                            pixels[y * size.X + x] = default;
                            continue;
                        }
                        var index = new Vector2i((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y));
                        if (index != previous)
                        {
                            color = default;
                            var chunkIndex = SharedMapSystem.GetChunkIndices(index, RadarTerrainChunk.Size);
                            var chunk = new RadarTerrainChunk(chunkIndex, step);
                            if (chunkIndex != previousChunk)
                            {
                                chunks.TryGetValue(chunk, out data);
                                previousChunk = chunkIndex;
                            }
                            if (data != null)
                            {
                                var offset = index - chunk.Origin;
                                color.PackedValue = data[offset.Y * RadarTerrainChunk.Size + offset.X];
                            }
                            previous = index;
                        }
                        pixels[y * size.X + x] = color;
                    }
                }
            }
            var middle = size.Y / 2;
            _terrainJob = Task.WhenAll(new Task[]
            {
                Task.Run(() => RenderRows(0, middle)),
                Task.Run(() => RenderRows(middle, size.Y))
            });
        }

        if (_terrainTexture != null)
            handle.DrawTextureRect(_terrainTexture, UIBox2.FromDimensions(Vector2.Zero, PixelSize));
    }

    protected void DrawPlanetMarkers(DrawingHandleScreen handle, Matrix3x2 worldToView, EntityUid? map)
    {
        var detection = EntManager.System<DetectionSystem>();
        var transform = EntManager.System<SharedTransformSystem>();
        var view = new Box2(Vector2.Zero, PixelSize);
        var query = EntManager.EntityQueryEnumerator<PlanetMapMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var marker, out var xform))
        {
            if (!detection.SharesRadarSpace(map, xform.MapUid))
                continue;

            var pos = Vector2.Transform(transform.GetWorldPosition(xform), worldToView);
            if (!view.Contains(pos))
                continue;

            for (var i = 0; i < MarkerShape.Length; i++)
                _markerVerts[i] = pos + MarkerShape[i] * UIScale;

            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _markerVerts.AsSpan(0, MarkerShape.Length), marker.Color.WithAlpha(0.35f));
            _markerVerts[MarkerShape.Length] = _markerVerts[0];
            handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, _markerVerts, marker.Color);

            if (!marker.ShowLabel)
                continue;

            var label = marker.Label is { } loc ? Loc.GetString(loc) : EntManager.GetComponent<MetaDataComponent>(uid).EntityName;
            if (string.IsNullOrEmpty(label))
                continue;

            var dims = handle.GetDimensions(Font, label, UIScale);
            handle.DrawString(Font, pos + new Vector2(6f * UIScale, -dims.Y / 2f), label, UIScale, marker.Color);
        }
    }
}
