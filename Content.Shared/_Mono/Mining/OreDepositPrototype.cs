using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Mining;

[Prototype("oreDeposit")]
public sealed partial class OreDepositPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public LocId Name;
    [DataField(required: true)] public ProtoId<MaterialPrototype> Material;
    [DataField] public Color Color = Color.Goldenrod.WithAlpha(0.5f);
}
