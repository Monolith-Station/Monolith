using System.IO;
using System.Linq;
using System.Text;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;

namespace Content.Server._Mono.Persistence;

public sealed partial class PersistentProfileSystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;

    private static DataNode Parse(string data)
    {
        using var reader = new StringReader(data);
        return DataNodeParser.ParseYamlStream(reader).Single().Root;
    }
}
