using Robust.Shared.Serialization;

namespace Content.Shared._Mono.MassFabricator;

[Serializable, NetSerializable]
public sealed class MassFabricatorViewEvent(bool enabled) : EntityEventArgs
{
    public readonly bool Enabled = enabled;
}
