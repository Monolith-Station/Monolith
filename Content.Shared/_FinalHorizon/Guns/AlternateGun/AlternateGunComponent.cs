using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalHorizon.Guns.AlternateGun;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AlternateGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = false;

    [DataField, AutoNetworkedField]
    public EntProtoId ActionToggleUnderbarrelId = "ActionToggleUnderbarrel";

    [DataField, AutoNetworkedField]
    public string Slot = "launcher";

    [DataField]
    public SoundSpecifier? SwitchSound = default;

    [DataField]
    public string EnableMessage = "underbarrel-generic-enable";

    [DataField]
    public string DisableMessage = "underbarrel-generic-disable";

    [DataField]
    public string EnabledMessage = "underbarrel-generic-enabled";

    [DataField]
    public string DisabledMessage = "underbarrel-generic-disabled";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionUnderbarrelToggle;
}
