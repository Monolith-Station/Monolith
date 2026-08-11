namespace Content.Shared._Forge.Silicons.StationAi;

[RegisterComponent]
public sealed partial class StationAiCameraRelayComponent : Component
{
    public const float DefaultRange = 5f;

    [DataField]
    public float Range = DefaultRange;
}
