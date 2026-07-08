/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// Marks a z-level map as a cloud layer: everything below it renders as a solid
/// cloud deck, while grids sitting on top of the layer stay crisp. Grids transiting
/// up toward the layer fog over gradually, white out completely at
/// ~ScalingViewport.CloudFullCoverDepth below it, then break through into clear air.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEZCloudLayerComponent : Component
{
    /// <summary>
    /// Base tint of the deck; the shader shades troughs darker and tops brighter.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color CloudColor = Color.FromHex("#aebfd4");
}
