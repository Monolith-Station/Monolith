using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Planets.Hellpods;

[Prototype("ceHellpodType")]
public sealed partial class CEHellpodTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Pod;

    [DataField(required: true)]
    public Dictionary<ProtoId<MaterialPrototype>, int> Materials = new();
}
