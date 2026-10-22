namespace Content.Shared._Rat.Overwatch;

/// <summary>
/// Компонент для ретрансляции звуков при наблюдении через камеру Overwatch. - Comp for relaying sounds when viewing overwatch cameras
/// </summary>
[RegisterComponent]
public sealed partial class RatOverwatchRelayedSoundComponent : Component
{
    /// <summary>
    /// Сущность ретранслируемого звука. - "The essence of the retransmitted sound." - thanks google translate i dont speak russian
    /// </summary>
    public EntityUid? Relay;
}
