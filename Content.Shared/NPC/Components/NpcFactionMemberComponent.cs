using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Mech.EntitySystems; // Frontier

namespace Content.Shared.NPC.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(NpcFactionSystem), typeof(SharedMechSystem)), AutoGenerateComponentState] // Frontier - Added MechSystem //Mono - autogeneratecomponentstate to replicate factions 2 clients
public sealed partial class NpcFactionMemberComponent : Component
{
    /// <summary>
    /// Factions this entity is a part of.
    /// </summary>
    [DataField, AutoNetworkedField] // Mono - needed for clientside music system to know which music to play
    public HashSet<ProtoId<NpcFactionPrototype>> Factions = new();

    /// <summary>
    /// Cached friendly factions.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<ProtoId<NpcFactionPrototype>> FriendlyFactions = new();

    /// <summary>
    /// Cached hostile factions.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<ProtoId<NpcFactionPrototype>> HostileFactions = new();

    /// <summary>
    /// Used to add friendly factions in prototypes.
    /// </summary>
    [DataField, ViewVariables]
    public HashSet<ProtoId<NpcFactionPrototype>>? AddFriendlyFactions;

    /// <summary>
    /// Used to add hostile factions in prototypes.
    /// </summary>
    [DataField, ViewVariables]
    public HashSet<ProtoId<NpcFactionPrototype>>? AddHostileFactions;
}
