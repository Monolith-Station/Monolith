namespace Content.Shared._Crescent.DroneControl;

/// <summary>
///     Allows an entity with HTN to receive drone control orders from a linked drone control console.
/// </summary>
[RegisterComponent]
public sealed partial class DroneControlComponent : Component
{
    [DataField]
    public bool Autolinked = false;

    [DataField]
    public string OrderKey = "DroneCommand";

    [DataField]
    public string TargetKey = "DroneTarget";
}
