using System.Numerics;
using System.Runtime.InteropServices;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Mono.Water;

/// <summary>
/// Draws the animated surface for <c>FloorWater</c> tiles.
/// </summary>
public sealed partial class WaterOverlay : GridOverlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly SharedMapSystem _mapSystem;
    private readonly SharedTransformSystem _xformSystem;
    private readonly EntityLookupSystem _lookup;

    private readonly ShaderInstance _shader;
    private readonly Texture _white;
    private const int MaxTilesPerBatch = 4096;

    private readonly List<DrawVertexUV2DColor> _vertices = new();
    private readonly List<ushort> _indices = new();

    /// <summary>
    /// Runtime tile id for <see cref="WaterTile"/>. Assigned by the tile definition manager at init
    /// and stable for the session, so it is resolved once on first draw.
    /// </summary>
    private ushort? _waterTileId;

    private static readonly ProtoId<ContentTileDefinition> WaterTile = "FloorWater";

    private static readonly ProtoId<ShaderPrototype> WaterShader = "MonoWater";

    public WaterOverlay()
    {
        IoCManager.InjectDependencies(this);

        _mapSystem = _entManager.System<SharedMapSystem>();
        _xformSystem = _entManager.System<SharedTransformSystem>();
        _lookup = _entManager.System<EntityLookupSystem>();

        _shader = _proto.Index(WaterShader).InstanceUnique();
        _white = Texture.White;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _waterTileId ??= _proto.Index(WaterTile).TileId;

        var grid = Grid;

        if (!_entManager.EntityExists(grid.Owner))
            return;

        var handle = args.WorldHandle;
        var tileSize = grid.Comp.TileSize;

        var enumerator = _mapSystem.GetTilesEnumerator(grid.Owner, grid.Comp, args.WorldBounds);
        var drew = false;

        _vertices.Clear();
        _indices.Clear();

        while (enumerator.MoveNext(out var tileRef))
        {
            if (tileRef.Tile.TypeId != _waterTileId.Value)
                continue;

            AddTile(grid, tileRef, _lookup.GetLocalBounds(tileRef, tileSize), tileSize);

            if (_vertices.Count < MaxTilesPerBatch * 4)
                continue;

            Flush(handle, grid.Owner, ref drew);
        }

        Flush(handle, grid.Owner, ref drew);

        if (!drew)
            return;

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);

        RequiresFlush = true;
    }

    private void AddTile(Entity<MapGridComponent> grid, TileRef tileRef, Box2 local, ushort tileSize)
    {
        var offset = (ushort) _vertices.Count;
        var mask = SideMask(grid, tileRef.GridIndices);

        _vertices.Add(Vertex(local.BottomLeft, tileSize, mask));
        _vertices.Add(Vertex(local.BottomRight, tileSize, mask));
        _vertices.Add(Vertex(local.TopRight, tileSize, mask));
        _vertices.Add(Vertex(local.TopLeft, tileSize, mask));

        _indices.Add(offset);
        _indices.Add((ushort) (offset + 1));
        _indices.Add((ushort) (offset + 2));
        _indices.Add(offset);
        _indices.Add((ushort) (offset + 2));
        _indices.Add((ushort) (offset + 3));
    }

    /// <summary>
    /// Which of the tile's neighbours are something other than water.
    /// </summary>
    private float SideMask(Entity<MapGridComponent> grid, Vector2i indices)
    {
        var mask = 0f;

        if (IsCoast(grid, indices + new Vector2i(-1, 0)))
            mask += 1f;

        if (IsCoast(grid, indices + new Vector2i(1, 0)))
            mask += 2f;

        if (IsCoast(grid, indices + new Vector2i(0, -1)))
            mask += 4f;

        if (IsCoast(grid, indices + new Vector2i(0, 1)))
            mask += 8f;

        if (IsCoast(grid, indices + new Vector2i(-1, -1)))
            mask += 16f;

        if (IsCoast(grid, indices + new Vector2i(1, -1)))
            mask += 32f;

        if (IsCoast(grid, indices + new Vector2i(-1, 1)))
            mask += 64f;

        if (IsCoast(grid, indices + new Vector2i(1, 1)))
            mask += 128f;

        return mask;
    }

    /// <summary>
    /// Whether the tile at these indices is known to be something other than water. Mostly here so we arent spamming the same TryGetTile check 200 times.
    /// </summary>
    private bool IsCoast(Entity<MapGridComponent> grid, Vector2i indices)
    {
        return _mapSystem.TryGetTile(grid.Comp, indices, out var tile) && tile.TypeId != _waterTileId!.Value;
    }

    private static DrawVertexUV2DColor Vertex(Vector2 local, ushort tileSize, float mask)
    {
        return new DrawVertexUV2DColor(local, Color.White)
        {
            UV = new Vector2(mask, 0f),
            UV2 = local / tileSize,
        };
    }

    private void Flush(DrawingHandleWorld handle, EntityUid gridUid, ref bool drew)
    {
        if (_vertices.Count == 0)
            return;

        if (!drew)
        {
            handle.SetTransform(_xformSystem.GetWorldMatrix(gridUid));
            handle.UseShader(_shader);
            drew = true;
        }

        handle.DrawPrimitives(
            DrawPrimitiveTopology.TriangleList,
            _white,
            CollectionsMarshal.AsSpan(_indices),
            CollectionsMarshal.AsSpan(_vertices)); // I have no idea how this isn't triggering a sandbox exception. I'm horrified.

        _vertices.Clear();
        _indices.Clear();
    }
}
