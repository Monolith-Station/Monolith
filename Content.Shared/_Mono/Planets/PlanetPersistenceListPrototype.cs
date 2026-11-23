using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

namespace Content.Shared._Mono.Planets;

[Prototype]
public sealed partial class PlanetPersistenceListPrototype : IPrototype
{
    public static readonly ProtoId<PlanetPersistenceListPrototype> DefaultSemi = "DefaultSemi";
    public static readonly ProtoId<PlanetPersistenceListPrototype> DefaultFull = "DefaultFull";

    [IdDataField] public string ID { get; private set; } = default!;
    [DataField] public PlanetEntityPersistenceDirectives Entities = new();
    [DataField] public List<PlanetComponentPersistenceDirectives> Components = new();
}

[DataDefinition]
public sealed partial class PlanetEntityPersistenceDirectives
{
    [DataField] public HashSet<EntProtoId> Keep = new(); // No-op on Full; keep these entities as-is on Semi.
    [DataField] public HashSet<EntProtoId> Reset = new(); // Reset these to their default prototype state. Useful for things like lathes.
    [DataField] public Dictionary<EntProtoId, EntProtoId> Replace = new(); // Reset directive, but replaces with a different entity. Useful primarily for filled lockers.
    [DataField] public HashSet<EntProtoId> Delete = new(); // No-op on Semi; don't even bother keeping these entities on Full.
}

[DataDefinition]
public sealed partial class PlanetComponentPersistenceDirectives
{
    // A supplied whitelist, even an empty one, replaces the blacklist for this block.
    [DataField] public HashSet<EntProtoId>? Whitelist;
    [DataField] public HashSet<EntProtoId> Blacklist = new();

    [DataField(customTypeSerializer: typeof(CustomHashSetSerializer<string, ComponentNameSerializer>))]
    public HashSet<string> Reset = new(); // Reset this component to the state on the item prototype.

    [DataField(customTypeSerializer: typeof(CustomHashSetSerializer<string, ComponentNameSerializer>))]
    public HashSet<string> Delete = new(); // Remove this component while serializing.

    // Keep/Replace are not here because I see literally no way they'd ever be relevant;
    // Semi keeps components on saved things at all times (Keep irrelevant),
    // and why/how the fuck would you even use a Replace directive here????

    // Feel free to prove me wrong.
}
