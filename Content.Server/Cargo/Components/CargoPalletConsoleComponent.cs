using Content.Server.Cargo.Systems;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server.Cargo.Components;

[RegisterComponent]
[Access(typeof(CargoSystem))]
public sealed partial class CargoPalletConsoleComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public ProtoId<StackPrototype> CashType = "Credit";

    // Frontier
    // The distance in a radius around the console to check for cargo pallets
    // Can be modified individually when mapping, so that consoles have a further reach
    [DataField("palletDistance")]
    public int PalletDistance = 8;

    // The whitelist that determines what goods can be sold.  Accepts everything if null.
    [DataField]
    public EntityWhitelist? Whitelist;
    // End Frontier
}
