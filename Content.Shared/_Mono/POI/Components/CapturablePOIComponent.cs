using Robust.Shared.GameStates;

namespace Content.Shared._Mono.POI.Components;

/// <summary>
/// Marks an entity as a capturable point of interest.
/// Stores the permanent ownership state of the POI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CapturablePOIComponent : Component
{
    /// <summary>
    /// Current owning faction/company of this POI.
    /// Example:
    /// TSF
    /// USSP
    /// Pirates
    /// Rogue
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public string? OwnerFaction;


    /// <summary>
    /// Current owner name.
    /// Used for ship deed ownership text.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public string? OwnerName;


    /// <summary>
    /// Display name of the captured entity.
    /// Used for shuttle deed naming.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public string? POIName;


    /// <summary>
    /// Current capture progress percentage.
    /// 0-100.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float CaptureProgress;


    /// <summary>
    /// True while a capture is active.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool IsBeingCaptured;
}