using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.BUI;

[NetSerializable, Serializable]
public sealed class CargoPalletConsoleInterfaceState : BoundUserInterfaceState
{
    /// <summary>
    /// estimated apraised value of all the entities on top of pallets on the same grid as the console
    /// </summary>
    public int Appraisal;

    /// <summary>
    /// number of entities on top of pallets on the same grid as the console
    /// </summary>
    public int Count;

    /// <summary>
    /// are the buttons enabled
    /// </summary>
    public bool Enabled;

    /// <summary>
    /// the multiplier for the given cargo sell
    /// </summary>
    public double TradeCrateMultiplier;

    /// <summary>
    /// the multiplier for the given cargo sell
    /// </summary>
    public double OtherMultiplier;

    public CargoPalletConsoleInterfaceState(int appraisal, int count, bool enabled, double tradeCrateMultiplier = 1, double otherMultiplier = 1)
    {
        Appraisal = appraisal;
        Count = count;
        Enabled = enabled;
        TradeCrateMultiplier = tradeCrateMultiplier;
        OtherMultiplier = otherMultiplier;
    }
}
