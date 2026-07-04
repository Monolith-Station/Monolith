using Content.Shared.Shuttles.Components;
using Robust.Shared.GameStates;

namespace Content.Client.Shuttles.Components;

/// <summary>
/// Client-side component that controls whether a shuttle will FTL with docked shuttles or automatically undock.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FTLLockComponent : Component
{
    /// <summary>
    /// Whether FTL lock is currently enabled
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}