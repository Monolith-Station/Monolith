using Robust.Shared.GameStates;
using System;

namespace Content.Shared._Mono.Detection;

/// <summary>
///     Component that allows an entity to store heat for infrared signature detection logic.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ThermalSignatureComponent : Component
{
    [DataField]
    public float StoredHeat = 0f;

    /// <summary>
    ///     Stored heat retains this portion of itself every next second.
    /// </summary>
    [DataField]
    public float HeatDissipation = 0.75f;

    /// <summary>
    ///     For grids, the combined stored heat of all entities on the grid.
    ///     For other entities, their stored heat.
    ///     Don't attempt to modify.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float TotalHeat = 0f;
}
