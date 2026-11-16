using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalHorizon.Guns.AlternateGun;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AlternateGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = false;

    [DataField, AutoNetworkedField]
    public string Slot = "launcher";

    [DataField]
    public SoundSpecifier? SwitchSound = default;

    [DataField]
    public string EnableMessage = "underbarrel-launcher-enable";

    [DataField]
    public string DisableMessage = "underbarrel-launcher-disable";

    [DataField]
    public string EnabledMessage = "underbarrel-launcher-enabled";

    [DataField]
    public string DisabledMessage = "underbarrel-launcher-disabled";
}
