using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Stealth.Components;

/// <remarks>
/// Unpredicted because the client mispredicts DamageIncreased. Ideally re-predict if this is fixed.
/// </remarks>
[RegisterComponent]
public sealed partial class RevealOnDamageComponent : Component
{
    /// <summary>
    /// What to modify the visibility by when damage is taken.
    /// </summary>
    [DataField]
    public float VisibilityOnDamage = 3f;

    /// <summary>
    /// The minimum damage needed to modify visibility.
    /// </summary>
    [DataField]
    public float DamageThreshold = 1f;

    /// <summary>
    /// A whitelist of damage types which, when taken, will modify the visibility.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> DamageTypes = new List<ProtoId<DamageTypePrototype>>() { "Blunt", "Slash", "Piercing", "Heat", "Shock" };
}