using Content.Shared._EinsteinEngines.Language;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.PDV.Components;

[RegisterComponent]
public sealed partial class HereticDetectorComponent : Component
{
    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "Freelance";

    [DataField]
    public LocId Message = "pdv-heretic-identified";

    [DataField]
    public ProtoId<LanguagePrototype> Language = "Freespeak";
}