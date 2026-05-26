using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Company;

/// <summary>
/// Component that checks if an entity belongs to a specific company before granting access.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(CompanyAccessReaderSystem))]
public sealed partial class CompanyAccessReaderComponent : Component
{
    /// <summary>
    /// The company ID that is required to access this entity.
    /// </summary>
    [DataField("requiredCompany")]
    public string RequiredCompany = string.Empty;

    /// <summary>
    /// Message to show when access is denied due to incorrect company.
    /// </summary>
    [DataField("popupMessage")]
    public string? PopupMessage = "company-tsf-access-denied";
}
