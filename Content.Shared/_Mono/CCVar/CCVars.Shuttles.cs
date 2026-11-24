using Robust.Shared.Configuration;

namespace Content.Shared._Mono.CCVar;

public sealed partial class MonoCVars
{
    /// <summary>
    /// Whether drive mode balances a shuttle's directional thrust.
    /// </summary>
    public static readonly CVarDef<bool> DriveThrustNormalizationEnabled =
        CVarDef.Create("mono.shuttle.drive_thrust_normalization_enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// How much drive mode moves each powered direction toward the average thrust (0 to 1).
    /// </summary>
    public static readonly CVarDef<float> DriveThrustNormalizationStrength =
        CVarDef.Create("mono.shuttle.drive_thrust_normalization_strength", 1f, CVar.SERVERONLY);
}
