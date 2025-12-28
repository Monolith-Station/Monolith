namespace Content.Shared._Goobstation.DoAfter;

/// <summary>
/// Event raised on the doafter user after a doafter ends.
/// </summary>
[ByRefEvent]
public readonly record struct DoAfterEndedEvent(EntityUid? Target, bool Cancelled);
