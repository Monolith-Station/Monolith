using Robust.Shared.Configuration;



[CVarDefs]
public sealed partial class BlackFlashCVars
{
    /// <summary>
    ///     Chance of a natural Black Flash, from 0 to 100. 0 basically disables natural ones and makes it cybernetic only.
    /// </summary>
    public static readonly CVarDef<float> BlackFlashChance =
        CVarDef.Create("mono.fun.blackflash_chance", 0.00005f, CVar.REPLICATED);

    /// <summary>
    ///     Damage multiplier of a natural Black Flash. The cybernetics basically use none of this as they have their own definitions.
    /// </summary>
    public static readonly CVarDef<float> DamageMultiplier =
        CVarDef.Create("mono.fun.blackflash_damage_mult", 2.5f, CVar.REPLICATED);

    public static readonly CVarDef<float> HydrakinChanceMultiplier =
        CVarDef.Create("mono.fun.blackflash_hydrakin_chance_multiplier", 5f, CVar.REPLICATED);
}
