/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float>
        CEBaseFallingDamage = CVarDef.Create("zlevels.ce_base_falling_damage", 0.75f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        CEBaseFallingOtherDamage = CVarDef.Create("zlevels.ce_base_falling_other_damage", 0.4f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        CEBaseFallingStunTime = CVarDef.Create("zlevels.ce_base_falling_stun_time", 0.1f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        CEBaseFallingOtherStunTime = CVarDef.Create("zlevels.ce_base_falling_other_stun_time", 0.06f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> ZLevelsPhysicsTickRate =
        CVarDef.Create("zlevels.ce_physics.tick_rate", 60, CVar.ARCHIVE);

    public static readonly CVarDef<bool> ZLevelsPhysicsClientSimulation =
        CVarDef.Create("zlevels.ce_physics.client_simulation", true, CVar.ARCHIVE | CVar.CLIENT);

    /**
     * Physics
     */

    public static readonly CVarDef<float>
        CEZLevelsPhysicsGravityForce = CVarDef.Create("ce.zlevels.physics.gravity_force", 9.8f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        CEZLevelsPhysicsVelocityLimit = CVarDef.Create("ce.zlevels.physics.velocity_limit", 20f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The minimum speed required to trigger LandEvent events.
    /// </summary>
    public static readonly CVarDef<float>
        CEZLevelsPhysicsImpactVelocity = CVarDef.Create("ce.zlevels.physics.impact_velocity", 3f, CVar.SERVER | CVar.REPLICATED);

    /**
     * Rendering
     */

    public static readonly CVarDef<int>
        CEZLevelsRenderingMaxZLevelsBelowRendering = CVarDef.Create("ce.zlevels.rendering.max_zLevels_below_rendering", 1, CVar.SERVER | CVar.REPLICATED);
}
