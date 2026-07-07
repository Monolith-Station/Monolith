namespace Content.Server._CE.ZLevels.Core.Components;

/// <summary>
/// Debug helper: smoothly lerps a transiting grid's absolute altitude to a target
/// value over a duration by driving SetTransitAltitude every tick. Removed
/// automatically when the lerp completes or the grid leaves transit.
/// </summary>
[RegisterComponent]
public sealed partial class CEZDebugAltitudeLerpComponent : Component
{
    [DataField]
    public float StartAltitude;

    [DataField]
    public float TargetAltitude;

    [DataField]
    public float Duration = 1f;

    [DataField]
    public float Elapsed;
}
