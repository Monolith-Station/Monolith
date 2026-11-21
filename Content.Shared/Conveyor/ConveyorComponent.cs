using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Conveyor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConveyorComponent : Component
{
    /// <summary>
    ///     The angle to move entities by in relation to the owner's rotation.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public Angle Angle = Angle.Zero;

    /// <summary>
    ///     The amount of units to move the entity by per second.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public float Speed = 2f;

    // Mono start
    /// <summary>
    ///     Amount of entities moved per departure.
    /// </summary>
    [DataField]
    public int BatchSize = 15;

    /// <summary>
    /// Interval between each batch departure.
    /// </summary>
    [DataField]
    public float DepartureInterval = 1f;
    // Mono end

    /// <summary>
    ///     The current state of this conveyor
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public ConveyorState State;

    [ViewVariables, AutoNetworkedField]
    public bool Powered;

    [DataField]
    public ProtoId<SinkPortPrototype> ForwardPort = "Forward";

    [DataField]
    public ProtoId<SinkPortPrototype> ReversePort = "Reverse";

    [DataField]
    public ProtoId<SinkPortPrototype> OffPort = "Off";

    // Mono start
    [ViewVariables(VVAccess.ReadOnly)]
    public int BatchRemaining = 0;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextDeparture = TimeSpan.Zero;

    /// <summary>
    /// Checks if this batch's limit has been reached, and prepares departure timespan for next batch if it has.
    /// </summary>
    /// <param name="curTime">IGameTiming.CurrentTime - current game time.</param>
    public void TryNextBatch(TimeSpan curTime)
    {
        if (BatchRemaining > 0) // do not reset if we still have a batch ongoing
            return;

        var nextDepartureWithInterval = NextDeparture + TimeSpan.FromSeconds(DepartureInterval); // departure for next batch is current next departure + interval in seconds
        NextDeparture = nextDepartureWithInterval > curTime ? nextDepartureWithInterval : curTime; // max(nextDepartureWithInterval, _timing.CurTime)
        BatchRemaining = BatchSize;
    }

    public void Reset()
    {
        NextDeparture = TimeSpan.Zero;
        BatchRemaining = 0;
    }
    // Mono end
}

[Serializable, NetSerializable]
public enum ConveyorVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum ConveyorState : byte
{
    Off,
    Forward,
    Reverse
}

