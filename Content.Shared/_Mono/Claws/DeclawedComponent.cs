using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Claws;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class DeclawedComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier? RawMeleeDamage = new DamageSpecifier();

    [DataField]
    public float DropChanceOnMelee = 0.03f;

    [DataField, AutoNetworkedField]
    public TimeSpan MaxItemHoldingTime =  TimeSpan.FromSeconds(30);

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ItemHoldingTime = TimeSpan.Zero;
}
