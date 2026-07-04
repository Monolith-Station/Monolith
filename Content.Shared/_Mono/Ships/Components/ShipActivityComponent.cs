using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Ships.Components;

/// <summary>
/// This is used for tracking ships that are inactive.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShipActivityComponent : Component
{
    [ViewVariables]
    public TimeSpan LastChecked { get; set; }

    [ViewVariables]
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public bool InactivePastThreshold { get; set; }

    [ViewVariables]
    public bool InactiveLastCheck { get; set; }

    [ViewVariables]
    public int TimesInactive { get; set; }

    [ViewVariables]
    public int InactiveThresholdMinutes { get; set; } = 10;

    public int GetSecondsInactive()
    {
        // The first time does not count as 5 seconds.
        var inactiveTimes = TimesInactive - 1;

        if (inactiveTimes <= 0)
            return 0;

        return inactiveTimes * (int) CheckInterval.TotalSeconds;
    }

    public int GetMinutesInactive()
    {
        // The first time does not count as 5 seconds.
        var inactiveTimes = TimesInactive - 1;

        if (inactiveTimes <= 0)
            return 0;

        var seconds = inactiveTimes * (int) CheckInterval.TotalSeconds;
        return seconds / 60;
    }
}
