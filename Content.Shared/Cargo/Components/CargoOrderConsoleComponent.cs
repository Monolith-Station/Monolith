using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Content.Shared._NF.Bank.Components;

namespace Content.Shared.Cargo.Components;

/// <summary>
/// Handles sending order requests to cargo. Doesn't handle orders themselves via shuttle or telepads.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CargoOrderConsoleComponent : Component
{
    [DataField("soundError")] public SoundSpecifier ErrorSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField("soundConfirm")]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    /// <summary>
    /// All of the <see cref="CargoProductPrototype.Group"/>s that are supported.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<string> AllowedGroups = new() { "market" };

    /// <summary>
    /// Radio channel on which order approval announcements are transmitted
    /// Mono: This is now only used if an order on AlertOnPurchase is made.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<RadioChannelPrototype> AnnouncementChannel = "Supply";

    // Frontier: station taxes
    // Accounts to receive tax value (each currently receives the entirety of the taxed value)
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<SectorBankAccount, float> TaxAccounts = new();
    // End Frontier

    /// <summary>
    /// A list of entity prototypes that the AnnouncementChannel will be alerted of the purchase of.
    /// </summary>
    [DataField]
    public List<EntProtoId> AlertOnPurchase = new List<EntProtoId>();
}
