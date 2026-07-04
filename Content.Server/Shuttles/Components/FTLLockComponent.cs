using Content.Server.Shuttles.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Analyzers;

namespace Content.Server.Shuttles.Components;

/// <summary>
/// Component that controls whether a shuttle will FTL with docked shuttles or automatically undock.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(ShuttleSystem), typeof(DockingSystem), Friend = AccessPermissions.ReadWriteExecute)]
public sealed partial class FTLLockComponent : Component
{
    /// <summary>
    /// Whether FTL lock is currently enabled
    /// </summary>
    [DataField, AutoNetworkedField]
    [Access(typeof(ShuttleConsoleSystem), Friend = AccessPermissions.ReadWriteExecute)]
    public bool Enabled = true;
}