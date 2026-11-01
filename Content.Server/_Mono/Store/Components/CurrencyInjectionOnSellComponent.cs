using Content.Shared._Mono.Company;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Store.Components;

[RegisterComponent]
public sealed partial class CurrencyInjectionOnSellComponent : Component
{
    [DataField(required: true)]
    public ProtoId<CompanyPrototype> Company;

    [DataField(required: true)]
    public Dictionary<string, FixedPoint2> Amount;

    /// <summary>
    /// If the object in question has a determined destination (such as a Cargo Depot), does this only happpen if the sell destination is correct?
    /// </summary>
    [DataField]
    public bool RequireCorrectDestination = true;
}