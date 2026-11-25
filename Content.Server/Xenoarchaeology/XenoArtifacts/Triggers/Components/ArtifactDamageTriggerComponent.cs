using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Triggers.Components;

/// <summary>
/// Triggers when a certain threshold of damage of certain types is reached
/// </summary>
[RegisterComponent]
public sealed partial class ArtifactDamageTriggerComponent : Component
{
    /// <summary>
    /// What damage types are accumulated for the trigger?
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>>? DamageTypes;

    /// <summary>
    /// What threshold has to be reached before it is activated?
    /// </summary>
    [DataField("damageThreshold", required: true)]
    public float DamageThreshold;

    /// <summary>
    /// How much damage has been accumulated on the artifact so far
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float AccumulatedDamage = 0;
}
