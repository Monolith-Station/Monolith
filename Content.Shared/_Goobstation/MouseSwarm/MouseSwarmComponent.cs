using System.Numerics;
using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Goobstation.MouseSwarm;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MouseSwarmComponent : Component
{
    [DataField]
    public ResPath Sprite = new("/Textures/Mobs/Animals/mouse.rsi");

    [DataField]
    public string State = "mouse-0";

    [DataField]
    public string? ExtraState;

    [DataField]
    public DamageSpecifier DamagePerMember = new() { DamageDict = new() { { "Slash", 1 } } };

    [DataField]
    public int MaxMembers = 25;

    [DataField]
    public float MaxOffset = 0.6f;

    [DataField, AutoNetworkedField]
    public List<MouseSwarmMember> Members = new();
}

[Serializable, NetSerializable, DataDefinition]
public partial record struct MouseSwarmMember
{
    [DataField]
    public ResPath Sprite;

    [DataField]
    public string State;

    [DataField]
    public string? ExtraState;

    [DataField]
    public Vector2 Offset;

    [DataField]
    public EntProtoId? Prototype;

    [DataField]
    public bool HasMicroBomb;

    // Snapshot of the member's factions at the time of absorption
    [DataField]
    public HashSet<ProtoId<NpcFactionPrototype>> Factions = new();

    public MouseSwarmMember(ResPath sprite, string state, Vector2 offset, EntProtoId? prototype, string? extraState = null, HashSet<ProtoId<NpcFactionPrototype>>? factions = null, bool hasMicroBomb = false)
    {
        Sprite = sprite;
        State = state;
        ExtraState = extraState;
        Offset = offset;
        Prototype = prototype;
        Factions = factions != null
            ? new HashSet<ProtoId<NpcFactionPrototype>>(factions)
            : new HashSet<ProtoId<NpcFactionPrototype>>();
        HasMicroBomb = hasMicroBomb;
    }
}