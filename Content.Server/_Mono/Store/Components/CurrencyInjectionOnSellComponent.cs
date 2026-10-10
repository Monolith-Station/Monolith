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
}