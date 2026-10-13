using Robust.Shared.GameStates;

namespace Content.Shared._FinalHorizon.Guns.AlternateGun;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AlternateGunAmmoProviderComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Slot = "launcher_mag";
}
