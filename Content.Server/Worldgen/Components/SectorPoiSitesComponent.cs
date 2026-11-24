using System.Numerics;
using Content.Server.Worldgen.Systems.Biomes;

namespace Content.Server.Worldgen.Components;

/// <summary>Placement footprints of the sector's starting grids, stored on the map.</summary>
[RegisterComponent, Access(typeof(BiomeSelectionSystem))]
public sealed partial class SectorPoiSitesComponent : Component
{
    internal List<SectorPoiSite> Sites = [];
}

internal readonly record struct SectorPoiSite(Vector2 Position, float ClearanceRadius, bool AllowsHostileDrones);

internal sealed record SectorAsteroidBelt(Box2i Bounds, IReadOnlyList<Vector2i> Chunks);
