namespace Content.Client.PDA;

/// <summary>
/// Used for visualizing PDA visuals.
/// </summary>
[RegisterComponent]
public sealed partial class PdaVisualsComponent : Component
{
    [DataField]
    public string? StateWithIdCard;

    public string? BorderColor;

    public string? AccentHColor;

    public string? AccentVColor;
}
