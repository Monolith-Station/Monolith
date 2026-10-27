using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Stealth.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RevealOnDamageComponent : Component
{
    /// <summary>
    /// What to modify the visibility by when damage is taken.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float VisibilityOnDamage = 3f;

    /// <summary>
    /// The minimum damage needed to modify visibility.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageThreshold = 1f;

    /// <summary>
    /// A whitelist of damage types which, when taken, will modify the visibility.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<DamageTypePrototype>> DamageTypes = new List<ProtoId<DamageTypePrototype>>() { "Blunt", "Slash", "Piercing", "Heat", "Shock" };
}