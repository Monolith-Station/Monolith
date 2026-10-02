using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.PDV.Components;

[RegisterComponent]
public sealed partial class AddComponentsAfterTimeComponent : Component
{
    /// <summary>
    /// The probability that this component will remove itself immediately.
    /// </summary>
    [DataField]
    public float SelectionProbability = 0.5f;

    [DataField]
    public TimeSpan MinSelectionTime = TimeSpan.FromMinutes(30);

    [DataField]
    public TimeSpan MaxSelectionTime = TimeSpan.FromMinutes(270); // 4.5 hours

    [DataField]
    public TimeSpan SelectionTime;

    [DataField(required: true)]
    public ComponentRegistry AddComponentsOnSelection;

    [DataField]
    public List<ProtoId<CompanyPrototype>> ExcludedCompanies = new List<ProtoId<CompanyPrototype>>();
}