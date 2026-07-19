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
    /// <summary>
    /// This is the current owner of the capturable grid; ie. TSF, USSP, PDV, VG, all other factions that should be able to capture a POI
    /// </summary>
    [Datafield]
    [AutoNetworkedField]
    public string? Owner;
    /// <summary>
    /// Set to True if the grid is currently in the process of being captured
    /// </summary>
    [Datafield]
    [AutoNetworkedField]
    public bool IsBeingCaptured;
    /// <summary>
    /// this is the state that updates after a capture attempt is successful, it should take only three values:
    ///  Neutral (no one touched it, the POI functions associated are turned off);
    ///  Captured (with precision of who captured it);
    ///  Hijacked (third mode of operation, a merc or spacer hijacked it with a regular id and as such it does not benefit to factions but prints cash/research disks)
    /// </summary>
    [Datafield]
    [AutoNetworkedField]
    public string? CaptureState;
    /// <summary>
    /// Tracks the progress of the POI capture up to 100
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float CaptureProgress;
    /// <summary>
    /// This dictates the class of the capturable grid, either an economic capture point, which sets off its own shenanigans when captured, or research capture point, which sets off its own shenanigans when captured
    /// (maybe add a third type that'd throw unique rewards aside from global tax revenue)
    /// </summary>
    [DataField]
    public string? CaptureClass ;

}
