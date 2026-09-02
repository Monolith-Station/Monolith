using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.PlantAnalyzer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class PlantAnalyzerComponent : Component
{
    [DataRecord]
    public partial struct PlantAnalyzerSettings
    {
        public TimeSpan ScanDelay = TimeSpan.FromSeconds(0.8);
        public TimeSpan ModeDelay = TimeSpan.FromSeconds(1);

        public PlantAnalyzerSettings()
        {
        }
    }

    [DataField]
    public PlantAnalyzerSettings Settings = new();

    public DoAfterId? DoAfter;

    [DataField]
    public SoundSpecifier? ScanningEndSound;

    [DataField]
    public SoundSpecifier? DeleteMutationEndSound;

    [DataField]
    public SoundSpecifier? ExtractEndSound;

    [DataField]
    public SoundSpecifier? InjectEndSound;

    public EntityUid? ScannedEntity;

    [DataField]
    public float MaxScanRange = 2.5f;

    [DataField, AutoNetworkedField]
    public PlantAnalyzerMode Mode;

    [DataField, AutoNetworkedField]
    public PlantGeneId Gene = PlantGeneId.NutrientConsumption;

    [DataField, AutoNetworkedField]
    public int DatabankIndex;

    public TimeSpan NextUpdate;

    [DataField]
    public List<PlantGeneData> GeneBank = new();

    [DataField]
    public List<PlantGasData> ConsumeGasBank = new();

    [DataField]
    public List<PlantGasData> ExudeGasBank = new();

    [DataField]
    public List<PlantChemicalData> ChemicalBank = new();
}
