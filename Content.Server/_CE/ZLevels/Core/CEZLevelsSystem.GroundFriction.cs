/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Maps;
using Content.Shared.Movement.Events;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    private const float GroundDragModifier = 20f;

    public const float GroundSkidDecel = 20f;

    public const float GroundSkidAngularDecel = 10f;

    private readonly Dictionary<EntityUid, (GameTick Tick, float Grip, bool Contact)> _groundCoverageCache = new();

    private void InitializeGroundFriction()
    {
        SubscribeLocalEvent<CEZGridFallerComponent, TileFrictionEvent>(OnGridTileFriction);
    }

    private void OnGridTileFriction(Entity<CEZGridFallerComponent> ent, ref TileFrictionEvent args)
    {
        var grip = GetGroundGrip(ent.Owner);
        if (grip <= 0f)
            return;

        args.Modifier *= 1f + (GroundDragModifier - 1f) * grip;
    }

    private void UpdateGroundFriction()
    {
        _groundCoverageCache.Clear();

        var query = EntityQueryEnumerator<CEZGridFallerComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            SetGroundContact(uid, HasGroundContact(uid));
        }
    }

    public float GetGroundGrip(EntityUid grid)
    {
        return GetGroundSample(grid).Grip;
    }

    public bool HasGroundContact(EntityUid grid)
    {
        return GetGroundSample(grid).Contact;
    }

    private (float Grip, bool Contact) GetGroundSample(EntityUid grid)
    {
        if (_groundCoverageCache.TryGetValue(grid, out var cached) && cached.Tick == _timing.CurTick)
            return (cached.Grip, cached.Contact);

        var sample = ComputeGroundSample(grid);
        _groundCoverageCache[grid] = (_timing.CurTick, sample.Grip, sample.Contact);
        return sample;
    }

    private (float Grip, bool Contact) ComputeGroundSample(EntityUid grid)
    {
        if (!_mapGridQuery.TryComp(grid, out var gridComp))
            return (0f, false);

        var mapUid = Transform(grid).MapUid;
        if (mapUid is not { } map || !_zMapQuery.HasComp(map) || !_mapGridQuery.TryComp(map, out var mapGrid) ||
            GetLocalAltitude(grid) > TouchdownProgress || !HasGroundUnderFootprint((grid, gridComp), map))
            return (0f, false);

        var gridMatrix = _transform.GetWorldMatrix(grid);
        var tileSize = gridComp.TileSize;

        var grip = 0f;
        var total = 0;

        var shipTiles = _map.GetAllTilesEnumerator(grid, gridComp);
        while (shipTiles.MoveNext(out var shipTile))
        {
            total++;

            var localCentre = new Vector2(
                (shipTile.Value.GridIndices.X + 0.5f) * tileSize,
                (shipTile.Value.GridIndices.Y + 0.5f) * tileSize);
            var worldPos = Vector2.Transform(localCentre, gridMatrix);

            grip += SampleGrip(map, mapGrid, worldPos);
        }

        return (total == 0 ? 0f : grip / total, true);
    }

    private float SampleGrip(EntityUid map, MapGridComponent mapGrid, Vector2 worldPos)
    {
        var local = _map.WorldToLocal(map, mapGrid, worldPos) / mapGrid.TileSize;
        var sampleX = local.X - 0.5f;
        var sampleY = local.Y - 0.5f;

        var x0 = (int) MathF.Floor(sampleX);
        var y0 = (int) MathF.Floor(sampleY);
        var fracX = sampleX - x0;
        var fracY = sampleY - y0;

        var bottom = MathHelper.Lerp(
            TileGrip(map, mapGrid, x0, y0),
            TileGrip(map, mapGrid, x0 + 1, y0),
            fracX);
        var top = MathHelper.Lerp(
            TileGrip(map, mapGrid, x0, y0 + 1),
            TileGrip(map, mapGrid, x0 + 1, y0 + 1),
            fracX);

        return MathHelper.Lerp(bottom, top, fracY);
    }

    private float TileGrip(EntityUid map, MapGridComponent mapGrid, int x, int y)
    {
        if (!_map.TryGetTileRef(map, mapGrid, new Vector2i(x, y), out var tileRef) || tileRef.Tile.IsEmpty)
            return 0f;

        var tile = (ContentTileDefinition) TilDefMan[tileRef.Tile.TypeId];
        return tile.GridFriction ?? tile.Friction;
    }
}
