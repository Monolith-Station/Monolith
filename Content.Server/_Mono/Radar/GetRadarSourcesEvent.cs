namespace Content.Server._Mono.Radar;

/// <summary>
/// Get a list of entities that we should check for distance against for showing blips.
/// If null, use just our console.
/// </summary>
[ByRefEvent]
public record struct GetRadarSourcesEvent(List<EntityUid>? Sources = null);
