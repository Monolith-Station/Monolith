using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Sensors.Events;

[Serializable, NetSerializable]
public sealed partial class SensorsConsoleRefreshEvent : EntityEventArgs {}
