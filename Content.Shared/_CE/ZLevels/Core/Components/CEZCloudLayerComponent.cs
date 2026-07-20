/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// Marks a Z-layer as a cloud layer. This occludes layers below so that the clients don't explode trying to render a million entities/tiles.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEZCloudLayerComponent : Component
{
    /// <summary>
    /// Base tint of the deck.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color CloudColor = Color.FromHex("#aebfd4");
}
