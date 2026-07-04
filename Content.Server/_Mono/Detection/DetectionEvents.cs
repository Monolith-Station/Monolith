namespace Content.Server._Mono.Detection;

/// <summary>
///     Raised on an entity to get its current heat/second generation for thermal signature purposes.
///     Do not rely on any grid entities' heat values while handling this event.
///     Negative values supported but may behave weirdly.
/// </summary>
[ByRefEvent]
public record struct GetThermalSignatureEvent(float Signature = 0f);
