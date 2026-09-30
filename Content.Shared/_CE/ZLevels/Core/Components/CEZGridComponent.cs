/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// Runtime membership marker added to a grid by <see cref="CEZGridConnectorSystem"/>.
/// Not persisted — always reconstructed from linker walls on map load.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class CEZGridComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public string NetworkId = string.Empty;

    /// <summary>
    /// Runtime cache — direct reference to the network manager entity.
    /// </summary>
    [ViewVariables]
    public EntityUid Network = EntityUid.Invalid;

    /// <summary>
    /// Tile-snapped XY-offset from the network's anchor.
    /// </summary>
    [ViewVariables]
    public Vector2 NetworkOffset = Vector2.Zero;

    /// <summary>
    /// Cached rotation (snapped to 90-degree quadrants).
    /// </summary>
    [ViewVariables]
    public Angle NetworkRotation = Angle.Zero;

    /// <summary>
    /// Cached fixture mass (updates on grid mass changes).
    /// </summary>
    [ViewVariables]
    public float CachedMass = 0f;
}
