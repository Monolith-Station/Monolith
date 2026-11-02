using Content.Shared._Mono.Store;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Store.Components;

[RegisterComponent]
public sealed partial class CurrencyInjectionOnBluespaceErrorComponent : Component
{
    [DataField(required: true)]
    public CurrencyInjection Injection;

    /// <summary>
    /// The percentage of value the grid must keep in order for the injection to occur.
    /// </summary>
    [DataField]
    public float IntegrityRequirement = 0.96f;

    /// <summary>
    /// All prototypes that MUST be present on the grid in order for the injection to occur.
    /// </summary>
    [DataField]
    public List<EntProtoId> RequiredEntities = [];
}