using Content.Shared._Mono.Company;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Store.Components;

[RegisterComponent]
public sealed partial class CurrencyInjectionOnBluespaceErrorComponent : Component
{
    [DataField(required: true)]
    public ProtoId<CompanyPrototype> Company;

    [DataField(required: true)]
    public Dictionary<string, FixedPoint2> Amount;

    /// <summary>
    /// The percentage of value the grid must keep in order for the injection to occur.
    /// </summary>
    [DataField]
    public float IntegrityRequirement = 0.96f;
}