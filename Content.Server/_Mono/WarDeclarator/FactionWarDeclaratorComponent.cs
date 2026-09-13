using Content.Shared._Mono.Company;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.WarDeclarator;

[RegisterComponent]
public sealed partial class FactionWarDeclaratorComponent : Component
{
    /// <summary>
    /// Which faction this declarator belongs to.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CompanyPrototype> Faction;

    /// <summary>
    /// Which channel to announce that this device has been used over.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "Common";

    /// <summary>
    /// The message to announce over the radio when the war declarator is used.
    /// </summary>
    [DataField(required: true)]
    public LocId WarDeclarationMessage;

    /// <summary>
    /// The message to tell the user when the war declarator is used by the wrong faction.
    /// </summary>
    public LocId WarDeclarationFailedMessage = "war-declarator-failed-invalid-biometrics";
}