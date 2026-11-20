namespace Content.Shared._Mono.MAWC.Scrap;

/// <summary>
///     Raised on the target stack entity when it is merged with another.
/// </summary>
[ByRefEvent]
public readonly record struct StackMergeEvent(EntityUid TargetId, EntityUid ToBeMerged, int Amount);
