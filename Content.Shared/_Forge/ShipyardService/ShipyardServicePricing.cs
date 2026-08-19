using Content.Shared._NF.Shipyard.Prototypes;

namespace Content.Shared._Forge.ShipyardService;

public static class ShipyardServicePricing
{
    /// <summary>
    /// Full shuttle repair (work + occupancy) never exceeds this fraction of the listed price.
    /// </summary>
    public const float RepairMaxVesselFraction = 0.5f;

    public static float GetRepairMultiplier(IReadOnlyList<VesselClass> classes)
    {
        foreach (var vesselClass in classes)
        {
            if (vesselClass is VesselClass.Civilian or VesselClass.Kitchen)
                return 0.5f;
        }

        return 1f;
    }

    public static int GetRepairCap(int vesselPrice)
    {
        if (vesselPrice <= 0)
            return int.MaxValue;

        return Math.Max(1, (int) Math.Round(vesselPrice * RepairMaxVesselFraction));
    }

    public static int CapRepairWork(int workCost, int vesselPrice)
    {
        if (workCost <= 0)
            return 0;

        return Math.Min(workCost, GetRepairCap(vesselPrice));
    }

    public static int CapRepairTotal(int workCost, int occupancyFee, int vesselPrice)
    {
        var total = Math.Max(0, workCost) + Math.Max(0, occupancyFee);
        if (total <= 0)
            return 0;

        return Math.Min(total, GetRepairCap(vesselPrice));
    }

    public static float GetReinforceMultiplier(IReadOnlyList<VesselClass> classes)
    {
        var multiplier = 1f;
        foreach (var vesselClass in classes)
        {
            if (vesselClass == VesselClass.Expedition)
                multiplier = Math.Max(multiplier, 3f);
            else if (vesselClass is VesselClass.Science or VesselClass.Atmospherics or VesselClass.Civilian or VesselClass.Kitchen)
                multiplier = Math.Max(multiplier, 2f);
        }

        return multiplier;
    }

    public static float GetPartUpgradeMultiplier(IReadOnlyList<VesselClass> classes)
    {
        foreach (var vesselClass in classes)
        {
            if (vesselClass == VesselClass.Science)
                return 0.5f;
        }

        return 1f;
    }

    public static bool IsCombat(VesselClass vesselClass)
    {
        return vesselClass is
            VesselClass.Capital or
            VesselClass.Detainment or
            VesselClass.Fighter or
            VesselClass.Patrol or
            VesselClass.Pursuit or
            VesselClass.Mercenary or
            VesselClass.Syndicate or
            VesselClass.Pirate or
            VesselClass.Corvette or
            VesselClass.Frigate or
            VesselClass.Destroyer or
            VesselClass.Cruiser;
    }

    public static int ApplyMultiplier(int amount, float multiplier)
    {
        return (int) Math.Max(0, Math.Round(amount * multiplier));
    }
}
