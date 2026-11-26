using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Planets.Shields;

[Serializable, NetSerializable]
public sealed class CEShieldActivationEvent(NetEntity projector, GameTick tick, NetCoordinates origin,
    ResolvedSoundSpecifier sound, AudioParams audioParams, float flashDuration, float shakeDuration, float shakeAmplitude) : EntityEventArgs
{
    public readonly NetEntity Projector = projector;
    public readonly GameTick Tick = tick;
    public readonly NetCoordinates Origin = origin;
    public readonly ResolvedSoundSpecifier Sound = sound;
    public readonly AudioParams AudioParams = audioParams;
    public readonly float FlashDuration = flashDuration;
    public readonly float ShakeDuration = shakeDuration;
    public readonly float ShakeAmplitude = shakeAmplitude;
}

public sealed class CEShieldPredictedActivationEvent(CEShieldActivationEvent activation) : EntityEventArgs
{
    public readonly CEShieldActivationEvent Activation = activation;
}
