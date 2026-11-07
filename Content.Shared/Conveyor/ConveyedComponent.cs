using Robust.Shared.GameStates;

namespace Content.Shared.Conveyor;

/// <summary>
/// Indicates this entity is currently contacting a conveyor and will subscribe to events as appropriate.
/// </summary>
[RegisterComponent, NetworkedComponent] // Mono: temporarily removes AutoGenerateComponentState - pulling isn't predicted, and dirty is costly. add this back when pulling is actually predicted or this entire system is designed better
public sealed partial class ConveyedComponent : Component
{
    // TODO: Delete if pulling gets fixed.
    /// <summary>
    /// True if currently conveying.
    /// </summary>
    [DataField] // Mono: temporarily removes AutoNetworkedField - pulling is the only one that actually uses this var and it isn't predicted, and dirty starts getting costly when you get to fuckin 10000 entities doing that. add this back when pulling is actually predicted or this entire system is designed better
    public bool Conveying;

    // Mono start
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? CurrentConveyor;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan MoveReadyAt = TimeSpan.Zero;

    public bool Claimed() => CurrentConveyor != null && CurrentConveyor != default;
    public bool Matured(TimeSpan currentTime) => MoveReadyAt <= currentTime;

    public void SetCurrentBelt(Entity<ConveyorComponent> conveyorEnt, TimeSpan curTime)
    {
        var conveyorComp = conveyorEnt.Comp;
        CurrentConveyor = conveyorEnt;

        conveyorComp.TryNextBatch(curTime);
        MoveReadyAt = conveyorComp.NextDeparture > curTime ? conveyorComp.NextDeparture : curTime; // max(nextDeparture, currentTime)
        conveyorComp.BatchRemaining--;
    }
    // Mono end
}
