using Robust.Shared.Prototypes;

namespace Content.Shared.NPC.Prototypes;

/// <summary>
/// Contains data about this faction's relations with other factions.
/// </summary>
[Prototype]
public sealed partial class NpcFactionPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ProtoId<NpcFactionPrototype>> Friendly = new();

    /// <summary>
    /// Mono - List of neutral factions.
    /// </summary>
    [DataField]
    public List<ProtoId<NpcFactionPrototype>> Neutral = new();

    [DataField]
    public List<ProtoId<NpcFactionPrototype>> Hostile = new();

    /// <summary>
    /// Mono - Makes all factions "Hostile" by default if set to "True".
    /// </summary>
    [DataField]
    public bool DefaultHostile = false;
}

/// <summary>
/// Cached data for the faction prototype. Is modified at runtime, whereas the prototype is not.
/// </summary>
public record struct FactionData
{
    [ViewVariables]
    public HashSet<ProtoId<NpcFactionPrototype>> Friendly;

    [ViewVariables]
    public HashSet<ProtoId<NpcFactionPrototype>> Neutral; // Mono edit - better NpcFactionSystem usage.

    [ViewVariables]
    public HashSet<ProtoId<NpcFactionPrototype>> Hostile;

    [ViewVariables]
    public bool DefaultHostile; // Mono edit - better NpcFactionSystem usage.
}
