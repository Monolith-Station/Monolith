using System.ComponentModel;
using Robus.Shared.Gamestates;
/// <summary>
/// Marks an entity as a capturable point.
/// Stores the permanent ownership state of the grid.
/// </summary>
namespace Content.Shared._Mono.SectorCapture.Components;
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CapturableGridComponent : Component
{
    [Datafield]
    [AutoNetworkedField]
    public string? Owner;
    [Datafield]
    [AutoNetworkedField]
    public bool IsBeingCaptured;
    [Datafield]
    [AutoNetworkedField]
    public string? CaptureState;
    [DataField]
    [AutoNetworkedField]
    public float CaptureProgress;
    [DataField]
    public string? Class ;

}
