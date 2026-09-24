using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.PlantAnalyzer;

[Serializable, NetSerializable]
public sealed partial class PlantAnalyzerDoAfterEvent : SimpleDoAfterEvent
{
    public PlantAnalyzerMode Mode;
    public PlantGeneId Gene;
    public int DatabankIndex;

    public PlantAnalyzerDoAfterEvent(PlantAnalyzerMode mode, PlantGeneId gene, int databankIndex)
    {
        Mode = mode;
        Gene = gene;
        DatabankIndex = databankIndex;
    }
}
