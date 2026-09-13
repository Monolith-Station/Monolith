namespace Content.Shared._Mono.Speech;

/// <summary>
/// Raised on an entity when something happens that may cause it to speak.
/// </summary>
[ByRefEvent]
public record struct SpeechTriggerEvent(SpeechTrigger Trigger);