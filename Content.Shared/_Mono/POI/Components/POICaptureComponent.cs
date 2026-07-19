using Robust.Shared.GameStates;

namespace Content.Shared._Mono.POI.Components;

/// <summary>
/// Tracks an active capture attempt on a capturable POI.
/// This stores temporary capture state, while CapturablePOIComponent stores ownership.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class POICaptureComponent : Component
{
    /// <summary>
    /// Entity currently performing the capture.
    /// </summary>
    public EntityUid? CapturingEntity;


    /// <summary>
    /// ID card used to capture the POI.
    /// Receives the ShuttleDeedComponent when capture completes.
    /// </summary>
    public EntityUid? CapturingIdCard;


    /// <summary>
    /// Time when capture started.
    /// </summary>
    public TimeSpan CaptureStart;


    /// <summary>
    /// Time required to complete capture.
    /// </summary>
    public TimeSpan CaptureDuration = TimeSpan.FromMinutes(5);


    /// <summary>
    /// Last announced capture percentage.
    /// </summary>
    public int LastBroadcastPercent = -1;
}