using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Decals;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.Decals.Overlays;

/// <summary>
///     Groups decals that are visually identical for drawing — keeps one entry (newest id wins).
/// </summary>
internal readonly record struct IdenticalDecalKey(Vector2 Pos, string Id, int ZIndex, Angle Angle, Color? Color);

public sealed class DecalOverlay : GridOverlay
{
    private readonly SpriteSystem _sprites;
    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _prototypeManager;

    private readonly Dictionary<string, (Texture Texture, bool SnapCardinals)> _cachedTextures = new(64);
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, List<(uint Id, Decal Decal)>>> _preparedChunks = new();
    private readonly List<(uint Id, Decal Decal)> _decals = new();

        /// <summary>
        ///     Maximum decals drawn per 1×1 tile (drops lowest Z-index first). 0 = unlimited.
        /// </summary>
    public int MaxPerTileDraw { get; set; } = 16;

        /// <summary>
        ///     When true, decals with identical tile-space appearance only draw once (newest id).
        /// </summary>
    public bool RemoveIdenticalDuplicates { get; set; } = true;

    public DecalOverlay(
        SpriteSystem sprites,
        IEntityManager entManager,
        IPrototypeManager prototypeManager)
    {
        _sprites = sprites;
        _entManager = entManager;
        _prototypeManager = prototypeManager;
    }

    public void InvalidateGridChunks(EntityUid gridId, IEnumerable<Vector2i> chunkIndices)
    {
        if (!_entManager.TryGetComponent(gridId, out DecalGridComponent? gridComp))
            return;

        var chunkCollection = gridComp.ChunkCollection.ChunkCollection;
        if (!_preparedChunks.TryGetValue(gridId, out var gridCache))
        {
            gridCache = new Dictionary<Vector2i, List<(uint Id, Decal Decal)>>();
            _preparedChunks[gridId] = gridCache;
        }

        foreach (var index in chunkIndices)
        {
            if (!chunkCollection.TryGetValue(index, out var chunk))
            {
                gridCache.Remove(index);
                continue;
            }

            gridCache[index] = PrepareChunk(chunk);
        }
    }

    /// <summary>
    ///     Drops every prepared chunk so cap/dedup settings or prototype reloads take effect on the next draw.
    /// </summary>
    public void ClearPreparedCache()
    {
        _preparedChunks.Clear();
        _cachedTextures.Clear();
    }

    // Forge-Change-Start: drop prepared chunks when the client loses the grid.
    public void RemoveGrid(EntityUid gridId)
    {
        _preparedChunks.Remove(gridId);
    }
    // Forge-Change-End

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var owner = Grid.Owner;

        if (!_entManager.TryGetComponent(owner, out DecalGridComponent? decalGrid) ||
            !_entManager.TryGetComponent(owner, out TransformComponent? xform))
        {
            return;
        }

        if (xform.MapID != args.MapId)
            return;

        // Shouldn't need to clear cached textures unless the prototypes get reloaded.
        var handle = args.WorldHandle;
        var xformSystem = _entManager.System<TransformSystem>();
        var eyeAngle = args.Viewport.Eye?.Rotation ?? Angle.Zero;

        var gridAABB = xformSystem.GetInvWorldMatrix(xform).TransformBox(args.WorldBounds.Enlarged(1f));
        var chunkEnumerator = new ChunkIndicesEnumerator(gridAABB, SharedDecalSystem.ChunkSize);
        _decals.Clear();
        if (!_preparedChunks.TryGetValue(owner, out var gridCache))
        {
            gridCache = new Dictionary<Vector2i, List<(uint Id, Decal Decal)>>();
            _preparedChunks[owner] = gridCache;
        }

        while (chunkEnumerator.MoveNext(out var index))
        {
            var chunkIndex = index.Value;
            if (!gridCache.TryGetValue(chunkIndex, out var prepared))
            {
                if (!decalGrid.ChunkCollection.ChunkCollection.TryGetValue(chunkIndex, out var chunk))
                    continue;

                prepared = PrepareChunk(chunk);
                gridCache[chunkIndex] = prepared;
            }

            foreach (var item in prepared)
            {
                if (!gridAABB.Contains(item.Decal.Coordinates))
                    continue;

                _decals.Add(item);
            }
        }

        if (_decals.Count == 0)
            return;

        var (_, worldRot, worldMatrix) = xformSystem.GetWorldPositionRotationMatrix(xform);
        handle.SetTransform(worldMatrix);

        foreach (var (_, decal) in _decals)
        {
            if (!_cachedTextures.TryGetValue(decal.Id, out var cache))
            {
                // Nothing to cache someone messed up
                if (!_prototypeManager.TryIndex<DecalPrototype>(decal.Id, out var decalProto))
                {
                    continue;
                }

                cache = (_sprites.Frame0(decalProto.Sprite), decalProto.SnapCardinals);
                _cachedTextures[decal.Id] = cache;
            }

            var cardinal = Angle.Zero;

            if (cache.SnapCardinals)
            {
                var worldAngle = eyeAngle + worldRot;
                cardinal = worldAngle.GetCardinalDir().ToAngle();
            }

            var angle = decal.Angle - cardinal;

            if (angle.Equals(Angle.Zero))
                handle.DrawTexture(cache.Texture, decal.Coordinates, decal.Color);
            else
                handle.DrawTexture(cache.Texture, decal.Coordinates, angle, decal.Color);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private List<(uint Id, Decal Decal)> PrepareChunk(DecalGridComponent.DecalChunk chunk)
    {
        var prepared = new List<(uint Id, Decal Decal)>(chunk.Decals.Count);
        foreach (var pair in chunk.Decals)
        {
            prepared.Add((pair.Key, pair.Value));
        }

        if (prepared.Count == 0)
            return prepared;

        if (RemoveIdenticalDuplicates)
            DeduplicateIdenticalDuplicates(prepared);

        if (MaxPerTileDraw > 0)
            CullDenseTiles(prepared);

        prepared.Sort((x, y) =>
        {
            var zComp = x.Decal.ZIndex.CompareTo(y.Decal.ZIndex);
            if (zComp != 0)
                return zComp;

            return x.Id.CompareTo(y.Id);
        });

        return prepared;
    }

    /// <summary>
    ///     Same position + prototype + transform + color + Z only need one draw; keep newest (highest id).
    /// </summary>
    private static void DeduplicateIdenticalDuplicates(List<(uint Id, Decal Decal)> decals)
    {
        if (decals.Count < 2)
            return;

        decals.Sort(static (a, b) => b.Id.CompareTo(a.Id));

        var seen = new HashSet<IdenticalDecalKey>();
        var write = 0;

        for (var read = 0; read < decals.Count; read++)
        {
            var d = decals[read].Decal;
            var key = new IdenticalDecalKey(d.Coordinates, d.Id, d.ZIndex, d.Angle, d.Color);

            if (!seen.Add(key))
                continue;

            if (write != read)
                decals[write] = decals[read];

            write++;
        }

        if (write < decals.Count)
            decals.RemoveRange(write, decals.Count - write);
    }

    /// <summary>
    ///     Avoid drawing hundreds of stacked decals on the same tile (same GPU cost as full sort + overdraw).
    /// </summary>
    private void CullDenseTiles(List<(uint Id, Decal Decal)> decals)
    {
        var buckets = new Dictionary<Vector2i, List<(uint Id, Decal Decal)>>();

        foreach (var item in decals)
        {
            var tk = new Vector2i(
                (int) Math.Floor(item.Decal.Coordinates.X),
                (int) Math.Floor(item.Decal.Coordinates.Y));

            if (!buckets.TryGetValue(tk, out var list))
            {
                list = new List<(uint Id, Decal Decal)>();
                buckets[tk] = list;
            }

            list.Add(item);
        }

        decals.Clear();

        foreach (var list in buckets.Values)
        {
            if (list.Count > MaxPerTileDraw)
            {
                list.Sort(static (a, b) =>
                {
                    var z = a.Decal.ZIndex.CompareTo(b.Decal.ZIndex);
                    if (z != 0)
                        return z;

                    return a.Id.CompareTo(b.Id);
                });

                list.RemoveRange(0, list.Count - MaxPerTileDraw);
            }

            decals.AddRange(list);
        }
    }
}
