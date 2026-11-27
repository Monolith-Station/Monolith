using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.MassFabricator;

public enum MassFabricatorPrintState : byte
{
    Idle,
    Printing,
    Fading,
}

[Serializable, NetSerializable]
public sealed partial class MassFabricatorPrintDoAfterEvent : SimpleDoAfterEvent;
