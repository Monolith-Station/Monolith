using Robust.Shared.Serialization;

namespace Content.Shared._CE.Camera;

[Serializable, NetSerializable]
public sealed class RadialShakeEvent : EntityEventArgs
{
    public float Duration = 1.5f;
    public float Amplitude = 1f;
}
