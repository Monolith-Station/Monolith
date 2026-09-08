using Robust.Shared.Serialization;

namespace Content.Shared._CE.Camera;

[Serializable, NetSerializable]
public sealed class CEScreenFlashEvent : EntityEventArgs
{
    public float Duration = 1f;
    public Color Color = Color.White;
}
