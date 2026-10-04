using System.Numerics;
using Content.Shared._Mono.Mining;
using Content.Shared._Mono.Planets;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.Mining;

public sealed partial class BuriedOreSystem : EntitySystem
{
    [Dependency] private SharedBiomeSystem _biomes = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedOreScannerSystem _scanner = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BuriedOreComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<BuriedOreComponent> ent, ref ComponentStartup args)
    {
        var config = ent.Comp.Configuration;
        if (config.Spacing < 8 || config.MinimumGap < 0f || config.MinimumGap >= config.Spacing ||
            config.Chance < 0f || config.Chance > 1f)
            throw new ArgumentException("Invalid planet deposit spacing, gap, or chance.");
        foreach (var mineral in config.Minerals)
        {
            if (mineral.MinRadius <= 0f || mineral.MaxRadius < mineral.MinRadius ||
                mineral.MaxRadius * 2.8f + config.MinimumGap > config.Spacing ||
                mineral.MinReserves < 1 || mineral.MaxReserves < mineral.MinReserves ||
                mineral.Weight < 0f || mineral.Rate < 0f || mineral.MinSaturation < 0f ||
                mineral.MaxSaturation < mineral.MinSaturation || mineral.MaxSaturation > 1f)
                throw new ArgumentException($"Invalid planet deposit settings for {mineral.Deposit}.");
        }
        EnsureComp<OreDepositSurfaceComponent>(ent.Owner);
    }

    public bool TryGetSurface(EntityUid uid, out EntityUid surface)
        => _scanner.TryGetSurface(uid, out surface);

    public Vector2 GetPosition(EntityUid uid, EntityUid surface)
        => Vector2.Transform(_transform.GetWorldPosition(uid), _transform.GetInvWorldMatrix(surface));

    private static Vector2i GetCell(Vector2 position, int spacing)
        => new((int)MathF.Floor(position.X / spacing), (int)MathF.Floor(position.Y / spacing));

    public BuriedOreVein? GetVein(EntityUid surface, Vector2 position)
    {
        if (!TryComp<BuriedOreComponent>(surface, out var deposits))
            return null;
        var vein = GetCellVein(surface, deposits, GetCell(position, deposits.Configuration.Spacing));
        return vein != null && OreDepositGeometry.Contains(vein.Boundary, position) ? vein : null;
    }

    public void Scan(EntityUid surface, Vector2 position, float scanRange, List<BuriedOreVein> results)
    {
        results.Clear();
        if (!TryComp<BuriedOreComponent>(surface, out var deposits))
            return;
        var range = new Vector2(scanRange);
        var bounds = new Box2(position - range, position + range);
        var min = GetCell(position - range, deposits.Configuration.Spacing);
        var max = GetCell(position + range, deposits.Configuration.Spacing);
        for (var y = min.Y; y <= max.Y; y++)
        for (var x = min.X; x <= max.X; x++)
        {
            var vein = GetCellVein(surface, deposits, new Vector2i(x, y));
            if (vein != null && IntersectsScan(vein.Boundary, bounds))
                results.Add(vein);
        }
    }

    private static bool IntersectsScan(Vector2[] boundary, Box2 bounds)
    {
        var min = boundary[0];
        var max = min;
        foreach (var point in boundary)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return bounds.Intersects(new Box2(min, max));
    }

    private BuriedOreVein? GetCellVein(EntityUid surface, BuriedOreComponent deposits, Vector2i cell)
    {
        if (deposits.Cells.TryGetValue(cell, out var existing))
        {
            if (existing != null)
                existing.Cell = cell;
            return existing;
        }
        var vein = Generate(surface, deposits, cell);
        deposits.Cells.Add(cell, vein);
        return vein;
    }

    private BuriedOreVein? Generate(EntityUid surface, BuriedOreComponent deposits, Vector2i cell)
    {
        var config = deposits.Configuration;
        if (!TryComp<BiomeComponent>(surface, out var biome) || config.Minerals.Count == 0)
            return null;
        var hash = unchecked((uint)biome.Seed ^ (uint)cell.X * 0x9E3779B9u ^ (uint)cell.Y * 0x85EBCA6Bu);
        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        var random = new Random(unchecked((int)hash));
        if (random.NextDouble() >= config.Chance)
            return null;

        var totalWeight = 0f;
        foreach (var entry in config.Minerals)
            totalWeight += entry.Weight;
        if (totalWeight <= 0f)
            return null;
        var roll = (float)random.NextDouble() * totalWeight;
        PlanetOreDeposit? mineral = null;
        foreach (var candidate in config.Minerals)
        {
            roll -= candidate.Weight;
            if (roll >= 0f)
                continue;
            mineral = candidate;
            break;
        }
        if (mineral == null)
            return null;

        float Sample(float min, float max) => float.Lerp(min, max, (float)random.NextDouble());
        var radius = Sample(mineral.MinRadius, mineral.MaxRadius);
        var margin = radius * 1.4f + config.MinimumGap * 0.5f;
        var center = (Vector2)(cell * config.Spacing) +
            new Vector2(Sample(margin, config.Spacing - margin), Sample(margin, config.Spacing - margin));
        if (TryComp<ToroidalMapComponent>(surface, out var torus) &&
            (MathF.Abs(center.X) + margin > torus.Size / 2f || MathF.Abs(center.Y) + margin > torus.Size / 2f))
            return null;

        var grid = Comp<MapGridComponent>(surface);
        var index = new Vector2i((int)MathF.Floor(center.X / grid.TileSize), (int)MathF.Floor(center.Y / grid.TileSize));
        if (!_biomes.TryGetTile(index, biome.Layers, biome.Seed, (surface, grid), out var tile) ||
            tile.Value.IsEmpty || config.ExcludedTiles.Contains(_tiles[tile.Value.TypeId].ID))
            return null;

        const int points = 32;
        var boundary = new Vector2[points];
        var phase = Sample(0f, MathF.Tau);
        var stretch = Sample(0.75f, 1f);
        for (var i = 0; i < points; i++)
        {
            var angle = MathF.Tau * i / points;
            var distance = radius * (1f + 0.18f * MathF.Sin(3f * angle + phase) + 0.12f * MathF.Sin(5f * angle - phase));
            boundary[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * stretch) * distance;
        }
        var size = Math.Clamp((radius - mineral.MinRadius) / Math.Max(1f, mineral.MaxRadius - mineral.MinRadius), 0f, 1f);
        var reserves = Math.Max(1, (int)float.Lerp(mineral.MinReserves, mineral.MaxReserves, size));
        return new BuriedOreVein
        {
            Mineral = mineral.Deposit,
            Cell = cell,
            Boundary = boundary,
            Total = reserves,
            Remaining = reserves,
            InitialSaturation = Sample(mineral.MinSaturation, mineral.MaxSaturation),
            BaseRate = mineral.Rate,
        };
    }
}
