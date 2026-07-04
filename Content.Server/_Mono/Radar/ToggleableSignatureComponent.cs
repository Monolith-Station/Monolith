namespace Content.Server._Mono.Radar;

/// <summary>
///     Makes it possible to toggle this entity having a radar blip.
///     Don't use together with RadarBlipComponent, instead set Enabled to true and specify what blip you want it to have.
/// </summary>
[RegisterComponent]
public sealed partial class ToggleableSignatureComponent : Component
{
    /// <summary>
    ///     Whether its state can be seen on examine.
    /// </summary>
    [DataField]
    public bool Examinable = true;

    [DataField]
    public bool Enabled = false;

    [DataField]
    public RadarBlipComponent Blip = new();
}
