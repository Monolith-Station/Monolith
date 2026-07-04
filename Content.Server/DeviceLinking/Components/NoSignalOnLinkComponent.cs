using Content.Server.DeviceLinking.Systems;

namespace Content.Server.DeviceLinking.Components;

/// <summary>
/// When attached to an entity with a DeviceLinkSourceComponent,
/// prevents signals from being automatically sent when a new link is established.
/// </summary>
/// <remarks>
/// Useful for devices like shuttle console buttons that should only send signals
/// when explicitly triggered, not automatically when linked.
/// </remarks>
[RegisterComponent, Access(typeof(DeviceLinkSystem))]
public sealed partial class NoSignalOnLinkComponent : Component
{
}