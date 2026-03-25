using Robust.Shared.GameStates;

namespace Content.Shared.Light.Components;

/// <summary>
/// Assumes the entire attached grid is roofed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImplicitRoofComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.Black;
}