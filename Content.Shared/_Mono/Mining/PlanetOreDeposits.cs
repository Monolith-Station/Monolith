using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Mining;

[DataDefinition]
public sealed partial class PlanetOreDeposits
{
    [DataField] public int Spacing = 128;
    [DataField] public float MinimumGap = 24f;
    [DataField] public float Chance = 0.7f;
    [DataField] public HashSet<string> ExcludedTiles = ["FloorWater", "FloorNaturalLetoferol", "FloorChromite"];
    [DataField] public List<PlanetOreDeposit> Minerals = new();
}

[DataDefinition]
public sealed partial class PlanetOreDeposit
{
    [DataField(required: true)] public ProtoId<OreDepositPrototype> Deposit;
    [DataField] public float Weight = 1f;
    [DataField] public float MinRadius = 8f;
    [DataField] public float MaxRadius = 24f;
    [DataField] public int MinReserves = 2000;
    [DataField] public int MaxReserves = 10000;
    [DataField] public float MinSaturation = 0.4f;
    [DataField] public float MaxSaturation = 1f;
    [DataField] public float Rate = 4f;
}
