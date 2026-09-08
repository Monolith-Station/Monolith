using System.Numerics;
using System.Threading.Tasks;
using Content.Client._Mono.Radar;
using Content.Shared._Mono.Radar;
using Content.Shared._Mono.Planets;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Parallax.Biomes;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    public bool ShowTerrain { get; set; } = true;

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

    private float GetRadarAltitude(EntityUid uid)
    {
        var observer = _consoleEntity ?? _coordinates?.EntityId;
        if (observer == null)
            return 0f;

        var levels = EntManager.System<CESharedZLevelsSystem>();
        return levels.GetAbsoluteAltitude(uid) - levels.GetAbsoluteAltitude(observer.Value);
    }

    private float? GetStackRadarAltitude(EntityUid uid)
    {
        var map = EntManager.GetComponent<TransformComponent>(uid).MapUid;
        return _detection.GetRadarNetwork(map) != null
            ? EntManager.System<CESharedZLevelsSystem>().GetAbsoluteAltitude(uid)
            : null;
    }

    private void DrawTerrain(DrawingHandleScreen handle, Matrix3x2 worldToView, Vector2 center, EntityUid? map)
    {
        if (!ShowTerrain)
            return;

        Entity<MapGridComponent>? surface = null;
        var nearest = float.NegativeInfinity;
        var query = EntManager.AllEntityQueryEnumerator<BiomeComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out _, out var grid))
        {
            if (!_detection.SharesRadarSpace(map, uid))
                continue;

            var altitude = GetRadarAltitude(uid);
            if (altitude > 0.05f || altitude < nearest)
                continue;

            nearest = altitude;
            surface = (uid, grid);
        }

        if (surface is not { } terrain)
            return;

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
        var terrainToView = _transform.GetWorldMatrix(terrain.Owner) * worldToView;
        if (_consoleEntity is not { } console || !Matrix3x2.Invert(terrainToView, out var viewToTerrain))
            return;
        var tileSize = terrain.Comp.TileSize;
        var halfWrap = EntManager.TryGetComponent<ToroidalMapComponent>(terrain.Owner, out var torus)
            ? torus.Size / (2f * tileSize)
            : float.PositiveInfinity;
        var bounds = viewToTerrain.TransformBox(new Box2(Vector2.Zero, size));
        bounds = new Box2(bounds.BottomLeft / tileSize, bounds.TopRight / tileSize);
        const int step = 1;
        var charts = EntManager.System<RadarTerrainSystem>();
        var revision = charts.Request(console, terrain.Owner, bounds, step);
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
}
