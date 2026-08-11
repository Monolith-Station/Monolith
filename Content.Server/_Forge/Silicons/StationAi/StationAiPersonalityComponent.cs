using Content.Shared._Forge.Silicons.StationAi;
using Robust.Shared.Network;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server._Forge.Silicons.StationAi;

/// <summary>
/// Server-owned identity state for a station AI brain.
/// </summary>
[RegisterComponent]
public sealed partial class StationAiPersonalityComponent : Component
{
    [DataField]
    public ProtoId<StationAiScreenPrototype> Screen = "StationAiScreenDefault";

    [DataField]
    public Color Color = Color.White;

    [ViewVariables]
    public NetUserId? Occupant;

    [ViewVariables]
    public EntityUid? OwnerMind;

    [ViewVariables]
    public string? PersonalityName;

    [ViewVariables]
    public TimeSpan NextCustomization;
}

[ByRefEvent]
public readonly record struct StationAiAvailabilityChangedEvent(bool Remove = false);
