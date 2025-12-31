using Content.Shared._EinsteinEngines.Language;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Implants.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class RattleComponent : Component
{
    // The radio channel the message will be sent to
    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "Syndicate";

    // Mono - Language of message on rattle
    [DataField]
    public ProtoId<LanguagePrototype> Language = "TauCetiBasic";

    // The message that the implant will send when crit
    [DataField]
    public LocId CritMessage = "deathrattle-implant-critical-message";

    // The message that the implant will send when dead
    [DataField("deathMessage")]
    public LocId DeathMessage = "deathrattle-implant-dead-message";
}
