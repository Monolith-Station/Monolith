using Content.Shared._Crescent.SpaceBiomes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Client._Crescent.SpaceBiomes;

[ByRefEvent]
public readonly record struct SpaceBiomeSwapMessage(ProtoId<SpaceBiomePrototype> Id);

[ByRefEvent]
public readonly record struct NewVesselEnteredMessage(
    string Name = "",
    string Description = "",
    string AmbientMusicPrototype = "");

[ByRefEvent]
public readonly record struct SpaceEnteredMessage;
