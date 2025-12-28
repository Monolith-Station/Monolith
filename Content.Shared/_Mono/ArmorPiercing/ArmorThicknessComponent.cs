using Robust.Shared.GameStates;

namespace Content.Shared._Mono.ArmorPiercing;

/// <summary>
/// Component that defines the armor thickness of entities.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ArmorThicknessComponent : Component
{
    /// <summary>
    /// The thickness value of this entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Thickness = 10;

    /// <summary>
    /// Whether this armor can be pierced at all.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanBePierced = true;
}
