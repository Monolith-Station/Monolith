using Content.Shared._Mono.Company;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Store.Components;

[RegisterComponent]
public sealed partial class CurrencyInjectionTargetComponent : Component
{
    /// <summary>
    /// Which company is this the injection target for?
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CompanyPrototype> Company;

    /// <summary>
    /// Which radio channel will be alerted of the injection?
    /// </summary>
    [DataField(required: true)]
    public ProtoId<RadioChannelPrototype> RadioChannel;

    /// <summary>
    /// What will be said over the radio channel?
    /// </summary>
    [DataField(required: true)]
    public LocId InjectionAlert;
}