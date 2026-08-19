using Content.Shared._NF.Shipyard.Prototypes;

namespace Content.Shared._Forge.ShipyardService;

public static class ShipyardServicePricing
{
    public static float GetRepairMultiplier(IReadOnlyList<VesselClass> classes)
    {
        var combat = false;
        var civilian = false;

        foreach (var vesselClass in classes)
        {
            if (IsCombat(vesselClass))
                combat = true;
            else if (vesselClass is VesselClass.Civilian or VesselClass.Kitchen)
                civilian = true;
        }

        if (combat)
            return 2f;
        if (civilian)
            return 0.5f;
        return 1f;
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
