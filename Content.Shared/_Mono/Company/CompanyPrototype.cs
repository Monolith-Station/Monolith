using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Company;

/// <summary>
/// Prototype for a company that can be assigned to players.
/// </summary>
[Prototype]
public sealed partial class CompanyPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The name of the company.
    /// </summary>
    [DataField(required: true)]
    public string Name { get; private set; } = default!;

    /// <summary>
    /// The description of the company.
    /// </summary>
    [DataField(required: false)]
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// The color used to display the company name in examine text.
    /// </summary>
    [DataField]
    public Color Color { get; private set; } = Color.Yellow;

    /// <summary>
    /// Whether this company should be disabled from selection in the UI.
    /// Companies with this set to true will still be assigned automatically through the job system,
    /// but players won't be able to select them manually.
    /// </summary>
    [DataField]
    public bool Disabled { get; private set; } = false;

    [DataField]
    public bool Whitelisted { get; private set; } = false;

    /// <summary>
    /// The image to display for this company in the UI.
    /// </summary>
    [DataField]
    public string? Image { get; private set; }
}
