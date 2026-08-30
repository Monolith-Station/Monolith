using Content.Shared.Atmos;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.PlantAnalyzer;

[Serializable, NetSerializable]
public enum PlantAnalyzerMode : byte
{
    Scan,
    DeleteMutations,
    Extract,
    Implant
}

[Serializable, NetSerializable]
public enum PlantGeneId : byte
{
    NutrientConsumption,
    WaterConsumption,
    IdealHeat,
    HeatTolerance,
    IdealLight,
    LightTolerance,
    ToxinsTolerance,
    LowPressureTolerance,
    HighPressureTolerance,
    PestTolerance,
    WeedTolerance,
    Endurance,
    Yield,
    Lifespan,
    Maturation,
    Production,
    GrowthStages,
    HarvestRepeat,
    Potency,
    Seedless,
    Viable,
    Ligneous,
    CanScream,
    TurnIntoKudzu,
    ConsumeGases,
    ExudeGases,
    Chemicals
}

[Serializable, NetSerializable, DataRecord]
public readonly partial record struct PlantGeneData(PlantGeneId Id, float Value);

[Serializable, NetSerializable, DataRecord]
public readonly partial record struct PlantGasData(Gas Gas, float Value);

[Serializable, NetSerializable, DataRecord]
public readonly partial record struct PlantChemicalQuantity(int Min, int Max, int PotencyDivisor, bool Inherent);

[Serializable, NetSerializable, DataRecord]
public readonly partial record struct PlantChemicalData(string Reagent, PlantChemicalQuantity Quantity);
