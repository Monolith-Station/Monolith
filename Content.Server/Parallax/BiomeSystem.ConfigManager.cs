// Mono - Refactored into smaller subsystems
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Parallax;

public sealed partial class BiomeSystem
{
    [Dependency] private ISerializationManager _biomeSerialization = default!;

    private void InitializeConfigManager()
    {
        // ConfigManager methods are now part of this partial class
    }

    private void ProtoReload(PrototypesReloadedEventArgs obj)
    {
        if (!obj.ByType.TryGetValue(typeof(BiomeTemplatePrototype), out var reloads))
            return;

        // Mono
        InvalidateCompiledBiomes();

        var query = AllEntityQuery<BiomeComponent>();

        while (query.MoveNext(out var uid, out var biome))
        {
            if (biome.Template == null || !reloads.Modified.TryGetValue(biome.Template, out var proto))
                continue;

            SetTemplate(uid, biome, (BiomeTemplatePrototype)proto);
        }
    }

    private void SetLoadRange(float obj)
    {
        // Round it up
        _loadRange = MathF.Ceiling(obj / ChunkSize) * ChunkSize;
        _loadArea = new Box2(-_loadRange, -_loadRange, _loadRange, _loadRange);
    }

    public void SetEnabled(Entity<BiomeComponent?> ent, bool enabled = true)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent, ent.Comp);
    }

    public void SetSeed(EntityUid uid, BiomeComponent component, int seed, bool dirty = true)
    {
        component.Seed = seed;
        InvalidateRadarTerrain(uid);

        if (dirty)
            Dirty(uid, component);
    }

    public void ClearTemplate(EntityUid uid, BiomeComponent component, bool dirty = true)
    {
        component.Layers.Clear();
        component.Template = null;
        InvalidateCompiledBiomes(); // Mono
        component.TemplateInitialized = false;
        component.PointOfInterestRooms.Clear();
        component.PointOfInterestTiles.Clear();
        InvalidateRadarTerrain(uid);

        if (dirty)
            Dirty(uid, component);
    }

    /// <summary>
    /// Sets the <see cref="BiomeComponent.Template"/> and refreshes layers.
    /// </summary>
    public void SetTemplate(EntityUid uid, BiomeComponent component, BiomeTemplatePrototype template, bool dirty = true)
    {
        var layers = CopyLayers(template, new HashSet<string>());
        component.Layers.Clear();
        component.Template = template.ID;
        component.Layers.AddRange(layers);
        component.TemplateInitialized = true;
        component.PointOfInterestRooms = new(template.PointOfInterestRooms);
        component.PointOfInterestTiles = new(template.PointOfInterestTiles);
        component.PointOfInterestSpacing = template.PointOfInterestSpacing;
        InvalidateRadarTerrain(uid);

        InvalidateCompiledBiomes(); // Mono

        if (dirty)
            Dirty(uid, component);
    }

    private List<IBiomeLayer> CopyLayers(BiomeTemplatePrototype template, HashSet<string> visiting)
    {
        if (!visiting.Add(template.ID))
            throw new InvalidOperationException($"Recursive biome template: {template.ID}");

        var layers = _biomeSerialization.CreateCopy(template.Layers, notNullableOverride: true);
        foreach (var layer in layers)
        {
            if (layer is BiomeMetaLayer meta)
                meta.Layers = CopyLayers(_proto.Index<BiomeTemplatePrototype>(meta.Template), visiting);
        }

        visiting.Remove(template.ID);
        return layers;
    }

    /// <summary>
    /// Adds the specified layer at the specified marker if it exists.
    /// </summary>
    public void AddLayer(EntityUid uid, BiomeComponent component, string id, IBiomeLayer addedLayer, int seedOffset = 0)
    {
        for (var i = 0; i < component.Layers.Count; i++)
        {
            var layer = component.Layers[i];

            if (layer is not BiomeDummyLayer dummy || dummy.ID != id)
                continue;

            var copy = _biomeSerialization.CreateCopy(addedLayer, notNullableOverride: true);
            if (copy is BiomeMetaLayer meta)
                meta.Layers = CopyLayers(_proto.Index<BiomeTemplatePrototype>(meta.Template), new HashSet<string>());
            copy.Noise.SetSeed(copy.Noise.GetSeed() + seedOffset);
            component.Layers.Insert(i, copy);
            InvalidateRadarTerrain(uid);
            break;
        }

        InvalidateCompiledBiomes(); // Mono

        Dirty(uid, component);
    }

    public void AddMarkerLayer(EntityUid uid, BiomeComponent component, string marker)
    {
        component.MarkerLayers.Add(marker);
        Dirty(uid, component);
    }

    /// <summary>
    /// Adds the specified template at the specified marker if it exists, withour overriding every layer.
    /// </summary>
    public void AddTemplate(EntityUid uid, BiomeComponent component, string id, BiomeTemplatePrototype template, int seedOffset = 0)
    {
        for (var i = 0; i < component.Layers.Count; i++)
        {
            var layer = component.Layers[i];

            if (layer is not BiomeDummyLayer dummy || dummy.ID != id)
                continue;

            var copies = CopyLayers(template, new HashSet<string>());
            for (var j = copies.Count - 1; j >= 0; j--)
            {
                var addedLayer = copies[j];
                addedLayer.Noise.SetSeed(addedLayer.Noise.GetSeed() + seedOffset);
                component.Layers.Insert(i, addedLayer);
            }

            InvalidateRadarTerrain(uid);

            break;
        }

        InvalidateCompiledBiomes(); // Mono

        Dirty(uid, component);
    }

    private void InvalidateRadarTerrain(EntityUid uid)
    {
        var ev = new Content.Shared._Mono.Radar.BiomeTerrainChangedEvent(uid);
        RaiseLocalEvent(ref ev);
    }
}
