namespace Content.Server._Mono.Cloning;

/// <summary>
/// Computer you link to a med scanner that actually puts the record into the grids database.
/// </summary>
[RegisterComponent]
public sealed partial class BodyScannerConsoleComponent : Component
{
    public const string ScannerPort = "MedicalScannerSender";

    [ViewVariables]
    public EntityUid? Scanner;

    /// <summary>
    /// Why are you looking up what "MaxDistance" does. What do you think.
    /// </summary>
    [DataField]
    public float MaxDistance = 4f;
}
