using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.CryoSleep.Components;

/// <summary>
/// Changes the radio channel that the cryosleep pod speaks to when this player enters cryosleep.
/// </summary>
[RegisterComponent]
public sealed partial class RadioOnCryosleepComponent : Component
{
    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "Common";
}