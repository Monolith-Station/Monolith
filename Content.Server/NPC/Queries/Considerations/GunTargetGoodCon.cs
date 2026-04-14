// Mono - whole file

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Identical to NPCCombatSystem.Ranged LOS check.
/// </summary>
public sealed partial class GunTargetGoodCon : UtilityConsideration
{
    [DataField]
    public float ShootThroughThreshold = 2f;
}
// TODO: rewrite HTN and make this not evil
