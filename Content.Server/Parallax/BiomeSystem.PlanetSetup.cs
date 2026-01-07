// Mono - Refactored into smaller subsystems
using Content.Server._Mono.Planets;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Parallax;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;
using ChunkIndicesEnumerator = Robust.Shared.Map.Enumerators.ChunkIndicesEnumerator;

namespace Content.Server.Parallax;

public sealed partial class BiomeSystem
{
    private void InitializePlanetSetup()
    {
        // PlanetSetup methods are now part of this partial class
    }

    /// <summary>
    /// Creates a simple planet setup for a map.
    /// </summary>
    public void EnsurePlanet(EntityUid mapUid, BiomeTemplatePrototype biomeTemplate, int? seed = null, MetaDataComponent? metadata = null, Color? mapLight = null)
    {
        if (!Resolve(mapUid, ref metadata))
            return;

        EnsureComp<MapGridComponent>(mapUid);
        EnsureComp<PlanetMapComponent>(mapUid);
        var biome = EntityManager.ComponentFactory.GetComponent<BiomeComponent>();
        seed ??= _random.Next();
        SetSeed(mapUid, biome, seed.Value, false);
        SetTemplate(mapUid, biome, biomeTemplate, false);
        AddComp(mapUid, biome, true);
        Dirty(mapUid, biome, metadata);

        var planetMap = EnsureComp<PlanetMapComponent>(mapUid);
        var parallax = EnsureComp<ParallaxComponent>(mapUid);
        parallax.Parallax = planetMap.Parallax;

        var gravity = EnsureComp<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(mapUid, gravity, metadata);

        // Day lighting
        // Daylight: #D8B059
        // Midday: #E6CB8B
        // Moonlight: #2b3143
        // Lava: #A34931
        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = mapLight ?? Color.FromHex("#D8B059");
        Dirty(mapUid, light, metadata);

        EnsureComp<RoofComponent>(mapUid);

        EnsureComp<LightCycleComponent>(mapUid);

        EnsureComp<SunShadowComponent>(mapUid);
        EnsureComp<SunShadowCycleComponent>(mapUid);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int)Gas.Oxygen] = 21.824779f;
        moles[(int)Gas.Nitrogen] = 82.10312f;

        var mixture = new GasMixture(moles, Atmospherics.T20C);

        _atmos.SetMapAtmosphere(mapUid, false, mixture);
    }

    /// <summary>
    /// Sets the specified tiles as relevant and marks them as modified.
    /// </summary>
    public void ReserveTiles(EntityUid mapUid, Box2 bounds, List<(Vector2i Index, Tile Tile)> tiles, BiomeComponent? biome = null, MapGridComponent? mapGrid = null)
    {
        if (!Resolve(mapUid, ref biome, ref mapGrid, false))
            return;

        foreach (var tileSet in _mapSystem.GetLocalTilesIntersecting(mapUid, mapGrid, bounds, false))
        {
            Vector2i chunkOrigin;
            HashSet<Vector2i> modified;

            // Existing, ignore
            if (_mapSystem.TryGetTileRef(mapUid, mapGrid, tileSet.GridIndices, out var existingRef) && !existingRef.Tile.IsEmpty)
            {
                chunkOrigin = SharedMapSystem.GetChunkIndices(tileSet.GridIndices, ChunkSize) * ChunkSize;
                modified = biome.ModifiedTiles.GetOrNew(chunkOrigin);
                modified.Add(tileSet.GridIndices);
                continue;
            }

            if (!TryGetBiomeTile(tileSet.GridIndices, biome.Layers, biome.Seed, (mapUid, mapGrid), out var tile))
            {
                continue;
            }

            chunkOrigin = SharedMapSystem.GetChunkIndices(tileSet.GridIndices, ChunkSize) * ChunkSize;
            modified = biome.ModifiedTiles.GetOrNew(chunkOrigin);
            modified.Add(tileSet.GridIndices);
            tiles.Add((tileSet.GridIndices, tile.Value));
        }

        _mapSystem.SetTiles(mapUid, mapGrid, tiles);
    }

    private void OnBiomeMapInit(EntityUid uid, BiomeComponent component, MapInitEvent args)
    {
        if (component.Seed == -1)
        {
            SetSeed(uid, component, _random.Next());
        }

        if (_proto.TryIndex(component.Template, out var biome))
            SetTemplate(uid, component, biome);

        var xform = Transform(uid);
        var mapId = xform.MapID;

        if (mapId != MapId.Nullspace && HasComp<MapGridComponent>(uid))
        {
            var setTiles = new List<(Vector2i Index, Tile tile)>();

            foreach (var grid in _mapManager.GetAllGrids(mapId))
            {
                if (!_fixturesQuery.TryGetComponent(grid.Owner, out var fixtures))
                    continue;

                // Don't want shuttles flying around now do we.
                _shuttles.Disable(grid.Owner);
                var pTransform = _physics.GetPhysicsTransform(grid.Owner);

                foreach (var fixture in fixtures.Fixtures.Values)
                {
                    for (var i = 0; i < fixture.Shape.ChildCount; i++)
                    {
                        var aabb = fixture.Shape.ComputeAABB(pTransform, i);

                        setTiles.Clear();
                        ReserveTiles(uid, aabb, setTiles);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Preloads biome for the specified area.
    /// </summary>
    public void Preload(EntityUid uid, BiomeComponent component, Box2 area)
    {
        var markers = component.MarkerLayers;
        var goobers = _markerChunks.GetOrNew(component);

        foreach (var layer in markers)
        {
            var proto = ProtoManager.Index(layer);
            var enumerator = new ChunkIndicesEnumerator(area, proto.Size);

            while (enumerator.MoveNext(out var chunk))
            {
                var chunkOrigin = chunk * proto.Size;
                var layerChunks = goobers.GetOrNew(proto.ID);
                layerChunks.Add(chunkOrigin.Value);
            }
        }
    }
}
