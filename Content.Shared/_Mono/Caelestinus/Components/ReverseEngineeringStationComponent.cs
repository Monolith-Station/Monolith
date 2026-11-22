using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Caelestinus.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReverseEngineeringStationComponent : Component
{
    /// <summary>
    /// The items this will output once the station has been given Y points in X category.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public Dictionary<ProtoId<ReverseEngineeringCategoryPrototype>, Dictionary<int, EntProtoId>> ProgressTree = new();

    /// <summary>
    /// The items this will output once the station has reached X overall progress.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public Dictionary<int, EntProtoId> TotalProgressTree = new();

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public Dictionary<ProtoId<ReverseEngineeringCategoryPrototype>, int> Progress = new();

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public int TotalProgress = 0;
}