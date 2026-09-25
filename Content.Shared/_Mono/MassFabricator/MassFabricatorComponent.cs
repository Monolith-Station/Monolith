using Robust.Shared.GameStates;

namespace Content.Shared._Mono.MassFabricator;

[RegisterComponent, NetworkedComponent]
public sealed partial class MassFabricatorComponent : Component
{
    [DataField]
    public float BlueprintLoad;

    [DataField]
    public float RemainingCharge;

    [DataField]
    public Color EyeColor = Color.FromHex("#379DFF");
}
