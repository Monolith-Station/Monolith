using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.ScuttleDevice;

[Serializable, NetSerializable]
public sealed partial class ScuttleArmDoAfterEvent : SimpleDoAfterEvent {}

[Serializable, NetSerializable]
public sealed partial class ScuttleDisarmDoAfterEvent : SimpleDoAfterEvent {}
