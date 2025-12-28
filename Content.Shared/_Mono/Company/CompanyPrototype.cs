using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Company;

/// <summary>
/// Prototype for a company that can be assigned to players.
/// </summary>
[Prototype("company")]
public sealed class CompanyPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The form / type of company ("type" is a bad word).
    /// Assign "Neutral"
    /// Assign "Protagonist"
    /// Assign "Antagonist"
    /// </summary>
    [DataField("form", required: true)]
    public string Form { get; private set; } = default!;


    /// <summary>
    /// The name of the company.
    /// </summary>
    [DataField("name", required: true)]
    public string Name { get; private set; } = default!;

    /// <summary>
    /// The description of the company.
    /// </summary>
    [DataField("description", required: false)]
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// The color used to display the company name in examine text.
    /// </summary>
    [DataField("color")]
    public Color Color { get; private set; } = Color.Yellow;

    /// <summary>
    /// Whether this company should be disabled from selection in the UI.
    /// Companies with this set to true will still be assigned automatically through the job system,
    /// but players won't be able to select them manually.
    /// </summary>
    [DataField("disabled")]
    public bool Disabled { get; private set; } = false;

    /// <summary>
    /// Access for login in closed company
    /// </summary>
    [DataField("logins", required: false)]
    public List<string> Logins { get; private set; } = new();

    /// <summary>
    /// The image to display for this company in the UI.
    /// </summary>
    [DataField("image")]
    public string? Image { get; private set; }
}
