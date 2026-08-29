namespace Content.Client._CE.Camera;

[RegisterComponent]
public sealed partial class RadialShakeComponent : Component
{
    public TimeSpan Start;
    public float Duration;
    public float Amplitude;
}
