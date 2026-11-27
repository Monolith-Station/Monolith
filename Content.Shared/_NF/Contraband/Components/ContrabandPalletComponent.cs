using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._NF.Contraband.Components;

[RegisterComponent]
[Access(typeof(SharedContrabandTurnInSystem))]
public sealed partial class ContrabandPalletConsoleComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("cashType", serverOnly: true)]
    public ProtoId<StackPrototype> RewardType = "FederationMilitaryCredit";

    [ViewVariables(VVAccess.ReadWrite), DataField(serverOnly: true)]
    public string Faction = "NFSD";

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public string LocStringPrefix = string.Empty;
}
