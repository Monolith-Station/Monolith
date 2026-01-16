using Content.Shared._Crescent.SpaceBiomes;
using Content.Shared.Salvage.Expeditions;
using Robust.Shared.Prototypes;

namespace Content.Client.Salvage;

[RegisterComponent]
public sealed partial class SalvageExpeditionComponent : SharedSalvageExpeditionComponent
{
    /// <summary>
    /// Mono: used for ContentAudioSystem.AmbientMusic.cs & SpaceBiomeSystem.cs to communicate biome on FTL
    /// </summary>
    [DataField]
    public ProtoId<SpaceBiomePrototype> Biome = "BiomeExpedition";
}
