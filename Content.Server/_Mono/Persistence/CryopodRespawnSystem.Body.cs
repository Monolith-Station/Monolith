using System.Globalization;
using System.IO;
using System.Linq;
using Content.Server._Mono.Planets;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mind;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server._Mono.Persistence;

public sealed partial class CryopodRespawnSystem
{
    [Dependency] private PlanetPersistenceSystem _planetPersistence = default!;
    [Dependency] private MapLoaderSystem _loader = default!;

    private bool TrySaveBody(EntityUid body, PlanetTypePrototype planet, out string snapshot)
    {
        snapshot = string.Empty;
        try
        {
            var serializer = _planetPersistence.SerializeEntityTree(planet, body,
                uid => HasComp<VirtualItemComponent>(uid) || HasComp<MindComponent>(uid));
            if (!serializer.EntityData.ContainsKey(serializer.GetYamlUid(body)) ||
                serializer.GetCategory() != FileCategory.Entity)
                return false;

            foreach (var (_, node) in serializer.EntityData.Values)
                CleanBodyEntity(node, serializer.EntityData);

            using var writer = new StringWriter();
            new YamlStream(new YamlDocument(serializer.Write().ToYamlNode()))
                .Save(new YamlMappingFix(new Emitter(writer)), assignAnchors: false);
            snapshot = writer.ToString();
            return true;
        }
        catch (Exception e)
        {
            Log.Warning($"Could not save cryopod body {ToPrettyString(body)}: {e.Message}");
            return false;
        }
    }

    public EntityUid? LoadBody(ReservationChoice choice)
    {
        try
        {
            using var reader = new StringReader(choice.Reservation.Body);
            return _loader.TryLoadEntity(reader, "cryopod reservation", out var entity,
                DeserializationOptions.Default with { LogInvalidEntities = false })
                ? entity.Value.Owner
                : null;
        }
        catch (Exception e)
        {
            Log.Warning($"Could not reload cryopod body {choice.Reservation.Id}: {e.Message}");
            return null;
        }
    }

    private static void CleanBodyEntity(MappingDataNode node, Dictionary<int, (EntityUid Uid, MappingDataNode Node)> entities)
    {
        if (node.Has("mapInit"))
            node.Remove("paused");

        if (!node.TryGet<SequenceDataNode>("components", out var components))
            return;

        for (var i = components.Count - 1; i >= 0; i--)
        {
            var component = (MappingDataNode) components[i];
            switch (component.Cast<ValueDataNode>("type").Value)
            {
                case "Actor":
                case "Mind":
                    components.RemoveAt(i);
                    break;
                case "MindContainer":
                    component.Remove("mind");
                    break;
                case "PlayerJob":
                    component.Remove("spawnStation");
                    component.Remove("spawnCoordinates");
                    break;
                case "ContainerContainer" when component.TryGet<MappingDataNode>("containers", out var containers):
                    foreach (var container in containers.Children.Values.OfType<MappingDataNode>())
                    {
                        if (container.TryGet<ValueDataNode>("ent", out var slot) && !Retained(slot))
                            container.Remove("ent");

                        if (!container.TryGet<SequenceDataNode>("ents", out var contents))
                            continue;

                        for (var j = contents.Count - 1; j >= 0; j--)
                        {
                            if (!Retained(contents[j]))
                                contents.RemoveAt(j);
                        }
                    }
                    break;
            }
        }

        bool Retained(DataNode entry) => entry is ValueDataNode value &&
            int.TryParse(value.Value, CultureInfo.InvariantCulture, out var id) && entities.ContainsKey(id);
    }
}
