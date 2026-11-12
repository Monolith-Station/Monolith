using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;
using Content.Shared._NF.Cloning;

namespace Content.Server._Mono.PDV.Components;

[RegisterComponent]
public sealed partial class AddComponentsAfterTimeComponent : Component, ITransferredByCloning
{
    /// <summary>
    /// The probability that this component will remove itself immediately.
    /// </summary>
    [DataField]
    public float SelectionProbability = 0.5f;

    // Don't make these longer than an hour
    [DataField]
    public TimeSpan MinSelectionTime = TimeSpan.FromMinutes(15);

    [DataField]
    public TimeSpan MaxSelectionTime = TimeSpan.FromMinutes(60);

    [DataField]
    public TimeSpan SelectionTime;

    [DataField(required: true)]
    public ComponentRegistry AddComponentsOnSelection;

    [DataField]
    public List<ProtoId<CompanyPrototype>> ExcludedCompanies = new List<ProtoId<CompanyPrototype>>();
}