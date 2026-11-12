using Content.Shared._Mono.Store;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Store.Components;

[RegisterComponent]
public sealed partial class CurrencyInjectionOnSellComponent : Component
{
    [DataField(required: true)]
    public CurrencyInjection Injection;

    /// <summary>
    /// If the object in question has a determined destination (such as a Cargo Depot), does this only happpen if the sell destination is correct?
    /// </summary>
    [DataField]
    public bool RequireCorrectDestination = true;
}