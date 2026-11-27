using System.Globalization;
using System.Linq;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Content.Shared._Mono.Planets;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Server._Mono.Planets;

public sealed partial class PlanetPersistenceSystem
{
    [Dependency] private IDependencyCollection _dependencies = default!;
    [Dependency] private IComponentFactory _components = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private ISharedAdminLogManager _stackAdminLog = default!;

    private static ResPath StackPath(ResPath folder) => folder / "map" / "stack.yml";

    private enum StackPolicy : byte
    {
        Omit,
        Keep,
        Reset,
    }

    private readonly record struct StackDirective(StackPolicy Policy, EntProtoId? Replacement = null);

    private void SaveStack(ResPath folder, PlanetTypePrototype planet, PlanetMapGenerationComponent generation)
    {
        var network = generation.Owner;
        var (serializer, resets) = BuildEntityTree(planet, generation.Layers.Values.Append(network), FileCategory.Save);
        var networkId = serializer.GetYamlUid(network);
        if (!serializer.EntityData.ContainsKey(networkId))
            throw new InvalidOperationException($"Planet {planet.ID} could not save its Z-level network.");

        var depths = new MappingDataNode();
        foreach (var (depth, map) in generation.Layers)
        {
            var id = serializer.GetYamlUid(map);
            if (!serializer.Maps.Contains(id) || !serializer.EntityData.ContainsKey(id))
                throw new InvalidOperationException($"Planet {planet.ID} could not save Z-level {depth}.");

            depths.Add(Format(depth), Format(id));
        }

        var stack = serializer.Write();
        stack.Add("planetStack", new MappingDataNode
        {
            { "surface", generation.Definition.Id },
            { "seed", Format(generation.Seed) },
            { "mode", planet.Persistence.ToString() },
            { "network", Format(networkId) },
            { "depths", depths },
            { "resets", resets },
        });
        WriteYaml(StackPath(folder), new YamlDocument(stack.ToYamlNode()));
    }

    internal EntitySerializer SerializeEntityTree(PlanetTypePrototype planet, EntityUid root, Predicate<EntityUid> omit)
        => BuildEntityTree(planet, [root], FileCategory.Entity, omit).Serializer;

    private (EntitySerializer Serializer, SequenceDataNode Resets) BuildEntityTree(
        PlanetTypePrototype planet, IEnumerable<EntityUid> roots, FileCategory category, Predicate<EntityUid>? omit = null)
    {
        var full = planet.Persistence == PlanetPersistenceMode.Full;
        var standalone = category == FileCategory.Entity;
        var keepByDefault = full || standalone;
        var list = _prototypes.Index(planet.PersistenceList ?? (full
            ? PlanetPersistenceListPrototype.DefaultFull
            : PlanetPersistenceListPrototype.DefaultSemi));
        var rules = list.Entities;
        var componentRules = list.Components;
        var filtered = componentRules.SelectMany(block => block.Reset.Concat(block.Delete))
            .Where(CanFilter).ToHashSet();
        var serializer = new EntitySerializer(_dependencies, SerializationOptions.Default with
        {
            Category = category,
            ErrorOnOrphan = !standalone,
            MissingEntityBehaviour = MissingEntityBehaviour.Ignore,
            EntityExceptionBehaviour = EntityExceptionBehaviour.IgnoreEntity,
        });
        var entityKeys = new EntityKeyWriter<Vector2i>(serializer);
        var storageKeys = new EntityKeyWriter<ItemStorageLocation>(serializer);
        serializer.SerializerProvider.RegisterSerializer(entityKeys);
        serializer.SerializerProvider.RegisterSerializer(storageKeys);

        var ordered = new List<(EntityUid Uid, StackDirective Directive)>();
        var live = new HashSet<EntityUid>();
        var policies = new Dictionary<string, StackDirective>();
        var componentPolicies = new Dictionary<string, (HashSet<string> Filtered, HashSet<string> Deleted)>();
        var rootSet = roots.ToHashSet();
        foreach (var root in rootSet)
            Collect(root, new(StackPolicy.Keep));

        if (filtered.Count > 0)
        {
            // Stack roots use the unfiltered serializer and its own prototype cache.
            if (!standalone)
            {
                serializer.SerializeEntities(rootSet.Where(live.Contains).ToHashSet());
                live.ExceptWith(rootSet);
            }
            serializer.PrototypeCache.Clear();

            var context = new ComponentWriteContext(_serialization, serializer, entityKeys, storageKeys);
            foreach (var name in filtered)
            {
                if (!_components.TryGetRegistration(name, out var registration))
                    throw new InvalidOperationException($"Unknown planet persistence component {name}.");

                var type = typeof(FilteredComponentWriter<>).MakeGenericType(registration.Type);
                Func<EntityPrototype?, bool> shouldFilter = proto => GetComponentPolicy(proto).Filtered.Contains(name);
                serializer.SerializerProvider.RegisterSerializer(Activator.CreateInstance(type, name, serializer, context, shouldFilter)!);
            }
        }

        serializer.SerializeEntities(live);
        var resets = new SequenceDataNode();
        var resetParents = new HashSet<EntityUid>();
        foreach (var (uid, directive) in ordered)
        {
            var id = serializer.GetYamlUid(uid);
            var parent = Transform(uid).ParentUid;
            var standaloneRoot = standalone && uid == ordered[0].Uid;
            var stackRoot = !standalone && rootSet.Contains(uid);
            if (!standaloneRoot && parent.IsValid() && (resetParents.Contains(parent) ||
                                    !serializer.YamlUidMap.TryGetValue(parent, out var parentId) ||
                                    !serializer.EntityData.ContainsKey(parentId)))
            {
                serializer.EntityData.Remove(id);
                serializer.Grids.Remove(id);
                continue;
            }

            var reset = directive.Policy == StackPolicy.Reset ||
                        !stackRoot && !standaloneRoot && serializer.ErroringEntities.Contains(uid) &&
                        !HasComp<MapComponent>(uid) && !HasComp<MapGridComponent>(uid);
            var proto = MetaData(uid).EntityPrototype;
            if (!reset)
            {
                if (filtered.Count > 0 && !stackRoot && !HasComp<MapComponent>(uid) && serializer.EntityData.TryGetValue(id, out var data))
                    ApplyComponentDirectives(data.Node, proto);

                continue;
            }

            if (proto == null)
                continue;

            try
            {
                if (directive.Replacement is { } replacement)
                {
                    if (full && rules.Delete.Contains(replacement))
                        continue;

                    if (!_prototypes.TryIndex(replacement, out var target) || !target.MapSavable ||
                        target.Components.ContainsKey("Map") || target.Components.ContainsKey("MapGrid"))
                        throw new InvalidOperationException($"Invalid planet replacement prototype {replacement}.");

                    proto = target;
                }

                var entry = ResetEntry(uid, id, serializer.GetYamlUid(parent));
                if (filtered.Count > 0)
                    ApplyComponentDirectives(entry, proto);

                serializer.EntityData.Add(id, (uid, entry));
                if (!serializer.Prototypes.TryGetValue(proto.ID, out var group))
                    serializer.Prototypes.Add(proto.ID, group = new());
                group.Add(id);
                resetParents.Add(uid);
                resets.Add(new ValueDataNode(Format(id)));
            }
            catch (Exception e)
            {
                _sawmill.Warning($"Could not reset {ToPrettyString(uid)} for planet persistence: {e.Message}");
            }
        }

        foreach (var group in serializer.Prototypes.Values)
            group.RemoveAll(id => !serializer.EntityData.ContainsKey(id));

        entityKeys.Trim();
        storageKeys.Trim();

        return (serializer, resets);

        static bool CanFilter(string name) => name is not ("MetaData" or "Transform" or "Map" or "MapGrid");

        (HashSet<string> Filtered, HashSet<string> Deleted) GetComponentPolicy(EntityPrototype? proto)
        {
            var id = proto?.ID ?? string.Empty;
            if (componentPolicies.TryGetValue(id, out var policy))
                return policy;

            var lineage = proto == null
                ? new HashSet<EntProtoId>()
                : _prototypes.EnumerateAllParents<EntityPrototype>(id, includeSelf: true)
                    .Select(parent => new EntProtoId(parent.id)).ToHashSet();
            policy = (new HashSet<string>(), new HashSet<string>());
            foreach (var block in componentRules)
            {
                if (block.Whitelist is { } whitelist ? !whitelist.Overlaps(lineage) : block.Blacklist.Overlaps(lineage))
                    continue;

                policy.Filtered.UnionWith(block.Reset.Where(CanFilter));
                policy.Deleted.UnionWith(block.Delete.Where(CanFilter));
            }

            policy.Filtered.UnionWith(policy.Deleted);
            componentPolicies.Add(id, policy);
            return policy;
        }

        void ApplyComponentDirectives(MappingDataNode node, EntityPrototype? proto)
        {
            var policy = GetComponentPolicy(proto);
            if (policy.Filtered.Count == 0)
                return;

            if (node.TryGet<SequenceDataNode>("components", out var components))
            {
                for (var i = components.Count - 1; i >= 0; i--)
                {
                    var name = ((MappingDataNode) components[i]).Cast<ValueDataNode>("type").Value;
                    if (policy.Filtered.Contains(name))
                        components.RemoveAt(i);
                }

                if (components.Count == 0)
                    node.Remove("components");
            }

            var missing = node.TryGet<SequenceDataNode>("missingComponents", out var previous)
                ? previous.Cast<ValueDataNode>().Select(component => component.Value).ToHashSet()
                : new HashSet<string>();

            missing.ExceptWith(policy.Filtered);
            if (proto != null)
                missing.UnionWith(proto.Components.Keys.Where(policy.Deleted.Contains));

            node.Remove("missingComponents");
            if (missing.Count > 0)
                node.Add("missingComponents", new SequenceDataNode(missing.Select(name => (DataNode) new ValueDataNode(name)).ToArray()));
        }

        void Collect(EntityUid uid, StackDirective directive)
        {
            if (directive.Policy == StackPolicy.Omit || TerminatingOrDeleted(uid) || !serializer.IsSerializable(uid))
                return;

            ordered.Add((uid, directive));
            // Reserve ids for resets so surviving entities can still refer to them.
            serializer.ReserveYamlId(uid);
            if (directive.Policy == StackPolicy.Reset)
                return;

            live.Add(uid);
            var resetContainers = (standalone || !rootSet.Contains(uid)) && filtered.Contains("ContainerContainer") &&
                                  HasComp<ContainerManagerComponent>(uid) &&
                                  GetComponentPolicy(MetaData(uid).EntityPrototype).Filtered.Contains("ContainerContainer");
            var children = Transform(uid).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (omit?.Invoke(child) == true || TerminatingOrDeleted(child) || HasComp<ActorComponent>(child) || HasComp<GhostComponent>(child) ||
                    TryComp<MindContainerComponent>(child, out var mind) && mind.HasMind)
                    continue;

                var metadata = MetaData(child);
                // Do not save contents as loose transform children when their container is reset.
                if (resetContainers && (metadata.Flags & MetaDataFlags.InContainer) != 0)
                    continue;

                var proto = metadata.EntityPrototype;
                if (full && proto != null && rules.Delete.Contains(proto.ID))
                    continue;

                var childPolicy = new StackDirective(keepByDefault ? StackPolicy.Keep : StackPolicy.Omit);
                if (proto != null && !policies.TryGetValue(proto.ID, out childPolicy))
                    policies.Add(proto.ID, childPolicy = GetPolicy(proto, rules, full, keepByDefault));

                if (HasComp<MapGridComponent>(child))
                {
                    if (childPolicy.Policy == StackPolicy.Reset)
                        continue;
                    childPolicy = new(StackPolicy.Keep);
                }

                Collect(child, childPolicy);
            }
        }
    }

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    private StackDirective GetPolicy(EntityPrototype prototype, PlanetEntityPersistenceDirectives rules, bool full, bool keepByDefault)
    {
        foreach (var (id, _) in _prototypes.EnumerateAllParents<EntityPrototype>(prototype.ID, includeSelf: true))
        {
            if (rules.Replace.TryGetValue(id, out var replacement))
                return new(StackPolicy.Reset, replacement);
            if (rules.Reset.Contains(id))
                return new(StackPolicy.Reset);
            if (!full && rules.Keep.Contains(id))
                return new(StackPolicy.Keep);
        }

        return new(keepByDefault ? StackPolicy.Keep : StackPolicy.Omit);
    }

    private MappingDataNode ResetEntry(EntityUid uid, int id, int parent)
    {
        var xform = Transform(uid);
        var transform = new MappingDataNode
        {
            { "type", "Transform" },
            { "parent", Format(parent) },
            { "pos", _serialization.WriteValue(xform.LocalPosition) },
            { "rot", _serialization.WriteValue(xform.LocalRotation) },
            { "anchored", xform.Anchored ? "true" : "false" },
        };
        return new MappingDataNode
        {
            { "uid", Format(id) },
            { "paused", MetaData(uid).EntityPaused ? "true" : "false" },
            { "components", new SequenceDataNode { transform } },
        };
    }

    internal (Entity<CEZMapNetworkComponent>? Network, Dictionary<int, EntityUid> Layers, List<EntityUid> Resets)?
        LoadStack(PlanetTypePrototype planet, PlanetMapPrototype definition, int seed)
    {
        if (planet.Persistence is not (PlanetPersistenceMode.Semi or PlanetPersistenceMode.Full) ||
            !_saves.TryGetValue(planet.ID, out var save) || save.Folder is not { } folder)
            return null;

        var path = StackPath(folder);
        LoadResult? loaded = null;
        try
        {
            var data = _resources.UserData;
            if (!data.Exists(path))
                return null;

            using var reader = data.OpenText(path);
            var stack = (MappingDataNode) DataNodeParser.ParseYamlStream(reader).Single().Root;
            var metadata = stack.Cast<MappingDataNode>("planetStack");
            if (metadata.Cast<ValueDataNode>("surface").Value != definition.ID ||
                metadata.Cast<ValueDataNode>("seed").Value != Format(seed) ||
                metadata.Cast<ValueDataNode>("mode").Value != planet.Persistence.ToString())
                throw new InvalidOperationException("Saved stack metadata does not match the planet.");

            var depths = metadata.Cast<MappingDataNode>("depths").Children.ToDictionary(
                pair => int.Parse(pair.Key, CultureInfo.InvariantCulture),
                pair => int.Parse(((ValueDataNode) pair.Value).Value, CultureInfo.InvariantCulture));
            if (depths.Count != definition.Layers.Count ||
                !Enumerable.Range(definition.BaseDepth, definition.Layers.Count).All(depths.ContainsKey))
                throw new InvalidOperationException("Saved depths do not match the planet's Z-levels. If the planet definition updated you may need to remove all backups.");

            var options = new MapLoadOptions
            {
                ExpectedCategory = FileCategory.Save,
                DeserializationOptions = DeserializationOptions.Default with { StoreYamlUids = true, LogInvalidEntities = false },
            };
            string? prototypeLoss = null;
            if (!_loader.TryLoadGeneric(stack, path.ToString(), out loaded, options))
            {
                prototypeLoss = OmitMissingPrototypeTrees(stack, depths.Values);
                if (prototypeLoss == null)
                    throw new InvalidOperationException("Map loader rejected the saved stack.");

                _sawmill.Warning($"Retrying planet stack {path} after prototype loss: {prototypeLoss}");
                if (!_loader.TryLoadGeneric(stack, path.ToString(), out loaded, options))
                    throw new InvalidOperationException("Map loader rejected the saved stack after omitting missing prototypes.");
            }

            var maps = loaded.Maps.ToDictionary(map => Comp<YamlUidComponent>(map).Uid, map => map.Owner);
            if (maps.Count != depths.Count || !depths.Values.ToHashSet().SetEquals(maps.Keys))
                throw new InvalidOperationException("Saved maps do not match the planet's Z-levels. If the planet definition updated you may need to remove all backups.");

            var layers = depths.ToDictionary(pair => pair.Key, pair => maps[pair.Value]);
            Entity<CEZMapNetworkComponent>? network = null;
            if (metadata.TryGet<ValueDataNode>("network", out var networkNode))
            {
                var networkId = int.Parse(networkNode.Value, CultureInfo.InvariantCulture);
                var uid = loaded.NullspaceEntities.Single(entity => Comp<YamlUidComponent>(entity).Uid == networkId);
                if (!TryComp<CEZMapNetworkComponent>(uid, out var zNetwork) ||
                    !TryComp<PlanetMapGenerationComponent>(uid, out var generation) ||
                    generation.Seed != seed || generation.Definition.Id != definition.ID ||
                    generation.Layers.Count != layers.Count ||
                    layers.Any(pair => generation.Layers.GetValueOrDefault(pair.Key) != pair.Value))
                    throw new InvalidOperationException("Saved Z-level network does not match the planet's maps.");

                network = (uid, zNetwork);
            }

            var resetIds = metadata.Cast<SequenceDataNode>("resets").Cast<ValueDataNode>()
                .Select(node => int.Parse(node.Value, CultureInfo.InvariantCulture)).ToHashSet();
            var resets = loaded.Entities.Where(uid => TryComp<YamlUidComponent>(uid, out var yaml) && resetIds.Contains(yaml.Uid)).ToList();
            if (prototypeLoss != null)
            {
                _stackAdminLog.Add(LogType.Action, LogImpact.High,
                    $"DEFCON 5 - Planet {planet.ID} loaded with prototype loss. Check server logs for {path}.");
                AlertAdmins("planet-prototype-loss-admin", planet.ID);
            }
            return (network, layers, resets);
        }
        catch (Exception e)
        {
            if (loaded != null)
                _loader.Delete(loaded);

            _sawmill.Error($"Could not reload planet stack at {path}: {e}");
            throw;
        }
    }

    private string? OmitMissingPrototypeTrees(MappingDataNode stack, IEnumerable<int> requiredMaps)
    {
        if (stack.Cast<MappingDataNode>("meta").Cast<ValueDataNode>("format").AsInt() != EntitySerializer.MapFormatVersion)
            return null;

        var migrations = new BeforeEntityReadEvent();
        RaiseLocalEvent(migrations);
        var groups = stack.Cast<SequenceDataNode>("entities");
        var omitted = new HashSet<int>();
        var missing = new List<string>();
        foreach (var group in groups.Cast<MappingDataNode>())
        {
            if (!group.TryGet<ValueDataNode>("proto", out var proto) || string.IsNullOrWhiteSpace(proto.Value))
                continue;

            var id = migrations.RenamedPrototypes.GetValueOrDefault(proto.Value, proto.Value);
            if (migrations.DeletedPrototypes.Contains(id) || _prototypes.HasIndex<EntityPrototype>(id))
                continue;

            var entries = group.Cast<SequenceDataNode>("entities");
            missing.Add($"{id} ({entries.Count})");
            foreach (var entry in entries.Cast<MappingDataNode>())
                omitted.Add(entry.Cast<ValueDataNode>("uid").AsInt());
        }

        if (omitted.Count == 0)
            return null;

        var children = new Dictionary<int, List<int>>();
        foreach (var group in groups.Cast<MappingDataNode>())
        foreach (var entry in group.Cast<SequenceDataNode>("entities").Cast<MappingDataNode>())
        {
            if (!entry.TryGet<SequenceDataNode>("components", out var components))
                continue;

            foreach (var component in components.Cast<MappingDataNode>())
            {
                if (component.Cast<ValueDataNode>("type").Value != "Transform" ||
                    !component.TryGet<ValueDataNode>("parent", out var parent) ||
                    !int.TryParse(parent.Value, CultureInfo.InvariantCulture, out var parentId))
                    continue;

                if (!children.TryGetValue(parentId, out var childIds))
                    children.Add(parentId, childIds = new());
                childIds.Add(entry.Cast<ValueDataNode>("uid").AsInt());
                break;
            }
        }

        var pending = new Queue<int>(omitted);
        while (pending.TryDequeue(out var parent))
        {
            if (!children.TryGetValue(parent, out var childIds))
                continue;

            foreach (var child in childIds)
            {
                if (omitted.Add(child))
                    pending.Enqueue(child);
            }
        }

        if (requiredMaps.Any(omitted.Contains) ||
            stack.Cast<SequenceDataNode>("maps").Cast<ValueDataNode>().Any(node => omitted.Contains(node.AsInt())))
            throw new InvalidOperationException($"Missing prototypes affect a saved map: {string.Join(", ", missing)}.");

        for (var i = groups.Count - 1; i >= 0; i--)
        {
            var entries = ((MappingDataNode) groups[i]).Cast<SequenceDataNode>("entities");
            for (var j = entries.Count - 1; j >= 0; j--)
            {
                if (omitted.Contains(((MappingDataNode) entries[j]).Cast<ValueDataNode>("uid").AsInt()))
                    entries.RemoveAt(j);
            }

            if (entries.Count == 0)
                groups.RemoveAt(i);
        }

        foreach (var key in new[] { "grids", "orphans", "nullspace" })
        {
            var ids = stack.Cast<SequenceDataNode>(key);
            for (var i = ids.Count - 1; i >= 0; i--)
            {
                if (omitted.Contains(((ValueDataNode) ids[i]).AsInt()))
                    ids.RemoveAt(i);
            }
        }

        return $"{omitted.Count} entities including descendants; missing prototypes: {string.Join(", ", missing)}";
    }

    private sealed class ComponentWriteContext(ISerializationManager manager, EntitySerializer serializer, params object[] serializers) : ISerializationContext
    {
        public SerializationManager.SerializerProvider SerializerProvider { get; } = CreateProvider(manager, serializer, serializers);
        public bool WritingReadingPrototypes => serializer.WritingReadingPrototypes;

        private static SerializationManager.SerializerProvider CreateProvider(ISerializationManager manager, EntitySerializer serializer, object[] serializers)
        {
            var provider = new SerializationManager.SerializerProvider(manager);
            provider.RegisterSerializer(serializer);
            foreach (var writer in serializers)
                provider.RegisterSerializer(writer);
            return provider;
        }
    }

    private sealed class FilteredComponentWriter<T>(string name, EntitySerializer serializer, ComponentWriteContext fallback,
        Func<EntityPrototype?, bool> shouldFilter) : ITypeWriter<T>
        where T : IComponent
    {
        public DataNode Write(ISerializationManager manager, T value, IDependencyCollection dependencies,
            bool alwaysWrite = false, ISerializationContext? context = null)
        {
            return serializer.CurrentComponent == name && serializer.CurrentEntity is { } entity &&
                   shouldFilter(entity.Comp.EntityPrototype)
                ? new MappingDataNode()
                : manager.WriteValue(value, alwaysWrite, fallback, notNullableOverride: true);
        }
    }

    // Omitted entities cannot share an "invalid" YAML key.
    private sealed class EntityKeyWriter<T>(EntitySerializer serializer) : ITypeWriter<Dictionary<EntityUid, T>>
    {
        private readonly List<(MappingDataNode Node, int Id)> _keys = new();

        public DataNode Write(ISerializationManager manager, Dictionary<EntityUid, T> value,
            IDependencyCollection dependencies, bool alwaysWrite = false, ISerializationContext? context = null)
        {
            var node = new MappingDataNode();
            foreach (var (uid, tile) in value)
            {
                if (serializer.YamlUidMap.TryGetValue(uid, out var id))
                {
                    node.Add(Format(id), manager.WriteValue(tile, alwaysWrite, context));
                    _keys.Add((node, id));
                }
            }

            return node;
        }

        public void Trim()
        {
            foreach (var (node, id) in _keys)
            {
                if (!serializer.EntityData.ContainsKey(id))
                    node.Remove(Format(id));
            }
        }
    }
}
