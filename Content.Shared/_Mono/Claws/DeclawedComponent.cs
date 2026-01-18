using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Claws;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class DeclawedComponent : Component
{
    [DataField]
    public DamageSpecifier? RawMeleeDamage = new DamageSpecifier();

    [DataField]
    public float DropChanceOnMelee = 0.05f;

    [DataField, AutoNetworkedField]
    public TimeSpan MaxItemHoldingTime =  TimeSpan.FromSeconds(30);

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ItemHoldingTime = TimeSpan.Zero;
}
