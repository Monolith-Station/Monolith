using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Caelestinus;

[Prototype]
[DataDefinition, Serializable, NetSerializable]
public sealed partial class ReverseEngineeringCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name", required: true)]
    public string Name { get; private set; } = default!;

    [DataField("examineText")]
    public LocId ExamineText = "caelestinus-reverse-engineering-examine";
}
