namespace Content.Server._Forge.Silicons.StationAi;

[ByRefEvent]
public record struct ResolveLocalSpeechOriginEvent(EntityUid Speaker, EntityUid Origin);
