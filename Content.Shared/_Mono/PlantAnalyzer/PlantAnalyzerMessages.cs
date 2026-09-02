using Robust.Shared.Serialization;

namespace Content.Shared._Mono.PlantAnalyzer;

[Serializable, NetSerializable]
public sealed class PlantAnalyzerSetMode(PlantAnalyzerMode mode, uint requestId) : BoundUserInterfaceMessage
{
    public PlantAnalyzerMode Mode { get; } = mode;
    public uint RequestId { get; } = requestId;
}

[Serializable, NetSerializable]
public sealed class PlantAnalyzerSelectGene(PlantGeneId gene, uint requestId) : BoundUserInterfaceMessage
{
    public PlantGeneId Gene { get; } = gene;
    public uint RequestId { get; } = requestId;
}

[Serializable, NetSerializable]
public sealed class PlantAnalyzerSelectDatabankEntry(int index, uint requestId) : BoundUserInterfaceMessage
{
    public int Index { get; } = index;
    public uint RequestId { get; } = requestId;
}

[Serializable, NetSerializable]
public sealed class PlantAnalyzerDeleteDatabankEntry : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class PlantAnalyzerRequestState : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class PlantAnalyzerControlState(
    PlantAnalyzerMode mode,
    uint modeRequestId,
    PlantGeneId gene,
    uint geneRequestId,
    int databankIndex,
    uint databankRequestId,
    PlantGeneData[] genes,
    PlantGasData[] consumedGases,
    PlantGasData[] exudedGases,
    PlantChemicalData[] chemicals) : BoundUserInterfaceMessage
{
    public PlantAnalyzerMode Mode { get; } = mode;
    public uint ModeRequestId { get; } = modeRequestId;
    public PlantGeneId Gene { get; } = gene;
    public uint GeneRequestId { get; } = geneRequestId;
    public int DatabankIndex { get; } = databankIndex;
    public uint DatabankRequestId { get; } = databankRequestId;
    public PlantGeneData[] Genes { get; } = genes;
    public PlantGasData[] ConsumedGases { get; } = consumedGases;
    public PlantGasData[] ExudedGases { get; } = exudedGases;
    public PlantChemicalData[] Chemicals { get; } = chemicals;
}
