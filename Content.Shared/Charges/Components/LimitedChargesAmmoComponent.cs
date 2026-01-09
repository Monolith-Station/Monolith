// Mono - whole file
using Content.Shared.Charges.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.Charges.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedChargesSystem))]
[AutoGenerateComponentState]
public sealed partial class LimitedChargesAmmoComponent : Component
{
    /// <summary>
    /// How many charges to refill, per item in stack (if stack)
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Charges = 1;

    /// <summary>
    /// What entities can we refill.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public EntityWhitelist Whitelist = new();
}
