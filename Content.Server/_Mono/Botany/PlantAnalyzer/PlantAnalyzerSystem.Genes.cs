using System.Linq;
using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Shared._Mono.PlantAnalyzer;
using Content.Shared.Atmos;

namespace Content.Server._Mono.Botany.PlantAnalyzer;

public sealed partial class PlantAnalyzerSystem
{
    private bool ExtractGene(Entity<PlantAnalyzerComponent> ent, EntityUid target, PlantGeneId gene)
    {
        if (!TryGetSeed(target, out var seed, out var tray) || !StoreGene(ent.Comp, seed, gene))
            return false;

        ClampDatabankIndex(ent.Comp);
        if (!tray)
            QueueDel(target);
        return true;
    }

    private bool ImplantGene(Entity<PlantAnalyzerComponent> ent, EntityUid target, int databankIndex)
    {
        if (databankIndex < 0 || databankIndex >= DatabankCount(ent.Comp) ||
            !TryGetMutableSeed(target, out var seed))
            return false;

        var index = databankIndex;
        if (index < ent.Comp.GeneBank.Count)
        {
            ApplyGene(seed, ent.Comp.GeneBank[index]);
            return true;
        }

        index -= ent.Comp.GeneBank.Count;
        if (index < ent.Comp.ConsumeGasBank.Count)
        {
            var gas = ent.Comp.ConsumeGasBank[index];
            seed.ConsumeGasses[gas.Gas] = gas.Value;
            return true;
        }

        index -= ent.Comp.ConsumeGasBank.Count;
        if (index < ent.Comp.ExudeGasBank.Count)
        {
            var gas = ent.Comp.ExudeGasBank[index];
            seed.ExudeGasses[gas.Gas] = gas.Value;
            return true;
        }

        index -= ent.Comp.ExudeGasBank.Count;
        if (index >= ent.Comp.ChemicalBank.Count)
            return false;

        var chemical = ent.Comp.ChemicalBank[index];
        seed.Chemicals[chemical.Reagent] = new SeedChemQuantity
        {
            Min = chemical.Quantity.Min,
            Max = chemical.Quantity.Max,
            PotencyDivisor = chemical.Quantity.PotencyDivisor,
            Inherent = chemical.Quantity.Inherent
        };
        return true;
    }

    private bool ClearMutations(EntityUid target)
    {
        if (!TryGetMutableSeed(target, out var seed) || seed.Mutations.Count == 0)
            return false;

        seed.Mutations.Clear();
        return true;
    }

    private bool TryGetMutableSeed(EntityUid target, out SeedData seed)
    {
        if (TryComp<SeedComponent>(target, out var packet))
        {
            SeedData? source = packet.Seed;
            if (source == null && packet.SeedId != null &&
                _prototypeManager.TryIndex(packet.SeedId, out SeedPrototype? prototype))
                source = prototype;

            if (source == null || source.Immutable)
            {
                seed = default!;
                return false;
            }

            seed = source.Unique ? source : source.Clone();
            packet.Seed = seed;
            return true;
        }

        if (TryComp<PlantHolderComponent>(target, out var holder) && holder.Seed != null && !holder.Seed.Immutable)
        {
            seed = holder.Seed.Unique ? holder.Seed : holder.Seed.Clone();
            holder.Seed = seed;
            return true;
        }

        seed = default!;
        return false;
    }

    private bool StoreGene(PlantAnalyzerComponent analyzer, SeedData seed, PlantGeneId geneId)
    {
        switch (geneId)
        {
            case PlantGeneId.ConsumeGases:
                foreach (var (gas, value) in seed.ConsumeGasses)
                {
                    var data = new PlantGasData(gas, value);
                    if (!analyzer.ConsumeGasBank.Contains(data))
                        analyzer.ConsumeGasBank.Add(data);
                }
                return seed.ConsumeGasses.Count > 0;
            case PlantGeneId.ExudeGases:
                foreach (var (gas, value) in seed.ExudeGasses)
                {
                    var data = new PlantGasData(gas, value);
                    if (!analyzer.ExudeGasBank.Contains(data))
                        analyzer.ExudeGasBank.Add(data);
                }
                return seed.ExudeGasses.Count > 0;
            case PlantGeneId.Chemicals:
                foreach (var (reagent, quantity) in seed.Chemicals)
                {
                    var data = new PlantChemicalData(reagent,
                        new PlantChemicalQuantity(quantity.Min, quantity.Max, quantity.PotencyDivisor, quantity.Inherent));
                    if (!analyzer.ChemicalBank.Contains(data))
                        analyzer.ChemicalBank.Add(data);
                }
                return seed.Chemicals.Count > 0;
            default:
                var gene = new PlantGeneData(geneId, ReadGene(seed, geneId));
                if (!analyzer.GeneBank.Contains(gene))
                    analyzer.GeneBank.Add(gene);
                return true;
        }
    }

    private static float ReadGene(SeedData seed, PlantGeneId gene)
        => gene switch
        {
            PlantGeneId.NutrientConsumption => seed.NutrientConsumption,
            PlantGeneId.WaterConsumption => seed.WaterConsumption,
            PlantGeneId.IdealHeat => seed.IdealHeat,
            PlantGeneId.HeatTolerance => seed.HeatTolerance,
            PlantGeneId.IdealLight => seed.IdealLight,
            PlantGeneId.LightTolerance => seed.LightTolerance,
            PlantGeneId.ToxinsTolerance => seed.ToxinsTolerance,
            PlantGeneId.LowPressureTolerance => seed.LowPressureTolerance,
            PlantGeneId.HighPressureTolerance => seed.HighPressureTolerance,
            PlantGeneId.PestTolerance => seed.PestTolerance,
            PlantGeneId.WeedTolerance => seed.WeedTolerance,
            PlantGeneId.Endurance => seed.Endurance,
            PlantGeneId.Yield => seed.Yield,
            PlantGeneId.Lifespan => seed.Lifespan,
            PlantGeneId.Maturation => seed.Maturation,
            PlantGeneId.Production => seed.Production,
            PlantGeneId.GrowthStages => seed.GrowthStages,
            PlantGeneId.HarvestRepeat => (float) seed.HarvestRepeat,
            PlantGeneId.Potency => seed.Potency,
            PlantGeneId.Seedless => seed.Seedless ? 1f : 0f,
            PlantGeneId.Viable => seed.Viable ? 1f : 0f,
            PlantGeneId.Ligneous => seed.Ligneous ? 1f : 0f,
            PlantGeneId.CanScream => seed.CanScream ? 1f : 0f,
            PlantGeneId.TurnIntoKudzu => seed.TurnIntoKudzu ? 1f : 0f,
            _ => 0f
        };

    private static void ApplyGene(SeedData seed, PlantGeneData gene)
    {
        switch (gene.Id)
        {
            case PlantGeneId.NutrientConsumption: seed.NutrientConsumption = gene.Value; break;
            case PlantGeneId.WaterConsumption: seed.WaterConsumption = gene.Value; break;
            case PlantGeneId.IdealHeat: seed.IdealHeat = gene.Value; break;
            case PlantGeneId.HeatTolerance: seed.HeatTolerance = gene.Value; break;
            case PlantGeneId.IdealLight: seed.IdealLight = gene.Value; break;
            case PlantGeneId.LightTolerance: seed.LightTolerance = gene.Value; break;
            case PlantGeneId.ToxinsTolerance: seed.ToxinsTolerance = gene.Value; break;
            case PlantGeneId.LowPressureTolerance: seed.LowPressureTolerance = gene.Value; break;
            case PlantGeneId.HighPressureTolerance: seed.HighPressureTolerance = gene.Value; break;
            case PlantGeneId.PestTolerance: seed.PestTolerance = gene.Value; break;
            case PlantGeneId.WeedTolerance: seed.WeedTolerance = gene.Value; break;
            case PlantGeneId.Endurance: seed.Endurance = gene.Value; break;
            case PlantGeneId.Yield: seed.Yield = (int) gene.Value; break;
            case PlantGeneId.Lifespan: seed.Lifespan = gene.Value; break;
            case PlantGeneId.Maturation: seed.Maturation = gene.Value; break;
            case PlantGeneId.Production: seed.Production = gene.Value; break;
            case PlantGeneId.GrowthStages: seed.GrowthStages = (int) gene.Value; break;
            case PlantGeneId.HarvestRepeat: seed.HarvestRepeat = (HarvestType) gene.Value; break;
            case PlantGeneId.Potency: seed.Potency = gene.Value; break;
            case PlantGeneId.Seedless: seed.Seedless = gene.Value != 0f; break;
            case PlantGeneId.Viable: seed.Viable = gene.Value != 0f; break;
            case PlantGeneId.Ligneous: seed.Ligneous = gene.Value != 0f; break;
            case PlantGeneId.CanScream: seed.CanScream = gene.Value != 0f; break;
            case PlantGeneId.TurnIntoKudzu: seed.TurnIntoKudzu = gene.Value != 0f; break;
        }
    }

    private void DeleteDatabankEntry(Entity<PlantAnalyzerComponent> ent)
    {
        var index = ent.Comp.DatabankIndex;
        if (index < 0 || index >= DatabankCount(ent.Comp))
            return;

        if (index < ent.Comp.GeneBank.Count)
            ent.Comp.GeneBank.RemoveAt(index);
        else if ((index -= ent.Comp.GeneBank.Count) < ent.Comp.ConsumeGasBank.Count)
            ent.Comp.ConsumeGasBank.RemoveAt(index);
        else if ((index -= ent.Comp.ConsumeGasBank.Count) < ent.Comp.ExudeGasBank.Count)
            ent.Comp.ExudeGasBank.RemoveAt(index);
        else
        {
            index -= ent.Comp.ExudeGasBank.Count;
            ent.Comp.ChemicalBank.RemoveAt(index);
        }

        ClampDatabankIndex(ent.Comp);
        DirtyField(ent, ent.Comp, nameof(PlantAnalyzerComponent.DatabankIndex));
    }

    private static int DatabankCount(PlantAnalyzerComponent comp)
        => comp.GeneBank.Count + comp.ConsumeGasBank.Count + comp.ExudeGasBank.Count + comp.ChemicalBank.Count;

    private static void ClampDatabankIndex(PlantAnalyzerComponent comp)
        => comp.DatabankIndex = Math.Clamp(comp.DatabankIndex, 0, Math.Max(0, DatabankCount(comp) - 1));

    private void SendScanState(Entity<PlantAnalyzerComponent> ent, SeedData seed, EntityUid target, bool tray)
    {
        var mutations = seed.MutationPrototypes
            .Select(id => _prototypeManager.TryIndex<SeedPrototype>(id, out var prototype) ? prototype.DisplayName : null)
            .Where(name => name != null)
            .Cast<string>()
            .ToArray();
        var state = new PlantAnalyzerScannedSeedPlantInformation
        {
            TargetEntity = GetNetEntity(target),
            IsTray = tray,
            SeedName = seed.DisplayName,
            SeedChem = seed.Chemicals.Keys.ToArray(),
            HarvestType = seed.HarvestRepeat switch
            {
                HarvestType.Repeat => AnalyzerHarvestType.Repeat,
                HarvestType.NoRepeat => AnalyzerHarvestType.NoRepeat,
                HarvestType.SelfHarvest => AnalyzerHarvestType.SelfHarvest,
                _ => AnalyzerHarvestType.Unknown
            },
            ExudeGases = GetGasFlags(seed.ExudeGasses.Keys),
            ConsumeGases = GetGasFlags(seed.ConsumeGasses.Keys),
            Endurance = seed.Endurance,
            SeedYield = seed.Yield,
            Lifespan = seed.Lifespan,
            Maturation = seed.Maturation,
            Production = seed.Production,
            GrowthStages = seed.GrowthStages,
            SeedPotency = seed.Potency,
            Speciation = mutations,
            NutrientConsumption = seed.NutrientConsumption,
            WaterConsumption = seed.WaterConsumption,
            IdealHeat = seed.IdealHeat,
            HeatTolerance = seed.HeatTolerance,
            IdealLight = seed.IdealLight,
            LightTolerance = seed.LightTolerance,
            ToxinsTolerance = seed.ToxinsTolerance,
            LowPressureTolerance = seed.LowPressureTolerance,
            HighPressureTolerance = seed.HighPressureTolerance,
            PestTolerance = seed.PestTolerance,
            WeedTolerance = seed.WeedTolerance,
            Mutations = GetMutationFlags(seed)
        };
        _ui.ServerSendUiMessage(ent.Owner, PlantAnalyzerUiKey.Key, state);
    }

    private static MutationFlags GetMutationFlags(SeedData seed)
    {
        var flags = MutationFlags.None;
        if (seed.TurnIntoKudzu) flags |= MutationFlags.TurnIntoKudzu;
        if (seed.Seedless || seed.PermanentlySeedless) flags |= MutationFlags.Seedless;
        if (seed.Ligneous) flags |= MutationFlags.Ligneous;
        if (seed.CanScream) flags |= MutationFlags.CanScream;
        if (!seed.Viable) flags |= MutationFlags.Unviable;
        return flags;
    }

    private static GasFlags GetGasFlags(IEnumerable<Gas> gases)
    {
        var flags = GasFlags.None;
        foreach (var gas in gases)
        {
            flags |= gas switch
            {
                Gas.Nitrogen => GasFlags.Nitrogen,
                Gas.Oxygen => GasFlags.Oxygen,
                Gas.CarbonDioxide => GasFlags.CarbonDioxide,
                Gas.Plasma => GasFlags.Plasma,
                Gas.Tritium => GasFlags.Tritium,
                Gas.WaterVapor => GasFlags.WaterVapor,
                Gas.Ammonia => GasFlags.Ammonia,
                Gas.NitrousOxide => GasFlags.NitrousOxide,
                Gas.Frezon => GasFlags.Frezon,
                _ => GasFlags.None
            };
        }
        return flags;
    }
}
