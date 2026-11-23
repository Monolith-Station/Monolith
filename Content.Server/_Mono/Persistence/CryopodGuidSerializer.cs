using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Server._Mono.Persistence;

public sealed class CryopodGuidSerializer : ITypeSerializer<Guid, ValueDataNode>
{
    public ValidationNode Validate(ISerializationManager manager, ValueDataNode node, IDependencyCollection dependencies,
        ISerializationContext? context = null) =>
        Guid.TryParse(node.Value, out _) ? new ValidatedValueNode(node) : new ErrorNode(node, "Invalid GUID.");

    public Guid Read(ISerializationManager manager, ValueDataNode node, IDependencyCollection dependencies,
        SerializationHookContext hookCtx, ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<Guid>? instanceProvider = null) =>
        Guid.Parse(node.Value);

    public DataNode Write(ISerializationManager manager, Guid value, IDependencyCollection dependencies,
        bool alwaysWrite = false, ISerializationContext? context = null) =>
        new ValueDataNode(value.ToString("D"));
}
