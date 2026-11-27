using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.StarSystem;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlanetTransitComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Planet;

    [DataField, AutoNetworkedField]
    public PlanetTransitDirection Direction;

    [DataField, AutoNetworkedField]
    public PlanetTransitPhase Phase;

    [DataField, AutoNetworkedField]
    public TimeSpan PhaseStart;

    [DataField, AutoNetworkedField]
    public TimeSpan PhaseEnd;

    [ViewVariables, AutoNetworkedField]
    public readonly HashSet<EntityUid> Grids = new();

    [ViewVariables]
    public readonly HashSet<EntityUid> OwnedPilotLocks = new();

    [ViewVariables]
    public readonly HashSet<EntityUid> InitiallyStatic = new();

    [ViewVariables]
    public bool Transferred;

    [ViewVariables]
    public bool Prepared;

    [ViewVariables]
    public bool OwnedDockLock;

    [ViewVariables]
    public EntityUid? TransitMap;

    [ViewVariables]
    public EntityUid? OriginMap;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlanetTransitMapComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid OriginMap;

    [DataField, AutoNetworkedField]
    public EntityUid Grid;

    [DataField, AutoNetworkedField]
    public PlanetTransitDirection Direction;

    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public TimeSpan End;

    [DataField, AutoNetworkedField]
    public bool Arrival;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlanetTransitFailureComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public TimeSpan End;

    [ViewVariables]
    public bool OwnedPilotLock;
}

public enum PlanetTransitDirection : byte
{
    Descent,
    Ascent,
}

public enum PlanetTransitPhase : byte
{
    Priming,
    Charging,
    Departing,
    Arriving,
}

[Serializable, NetSerializable]
public sealed class PlanetDescentRequestMessage : BoundUserInterfaceMessage
{
    public NetEntity Planet;
}
