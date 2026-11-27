using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Shipyard;

[Prototype]
public sealed partial class VesselReferenceCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name", required: true)]
    public string Name { get; private set; } = default!;

    [DataField("priority")]
    public int Priority { get; private set; }

    [DataField("componentTypes")]
    public List<string> ComponentTypes { get; private set; } = new();
}
