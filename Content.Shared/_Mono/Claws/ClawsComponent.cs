using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Claws;

/// <summary>
/// This is claw component used for <see cref="SharedClawsSystem"/> System.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ClawsComponent : Component
{
    [DataField, AutoNetworkedField]
    public int ClawStage;

    [DataField, AutoNetworkedField]
    public Dictionary<int, ClawStage> Stages = new();

    [DataField, AutoNetworkedField]
    public bool Declawed;

    [DataField]
    public TimeSpan GrowCooldown = TimeSpan.FromSeconds(1200);

    [DataField, AutoPausedField]
    public TimeSpan GrowTimer = TimeSpan.Zero;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ClawStage
{
    [DataField]
    public DamageSpecifier? Damage = new DamageSpecifier();

    [DataField]
    public float GunSpreadMultiplier = 1f;

    [DataField]
    public DamageModifierSet MeleeDamageModifiers = new DamageModifierSet();

    [DataField]
    public bool CanWideSwing;

    [DataField]
    public bool CanShoot = true;
}
