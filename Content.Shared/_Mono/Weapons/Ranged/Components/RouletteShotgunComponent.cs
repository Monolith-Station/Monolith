using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Weapons.Ranged.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class RouletteShotgunComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool BarrelRemoved;

    [DataField]
    public float BarrelDamageMultiplier = 2f;
}
