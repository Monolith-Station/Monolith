using Robust.Shared.Serialization;

namespace Content.Client._Crescent.SpaceBiomes;

[ByRefEvent]
public readonly record struct SpaceBiomeSwapMessage(string Biome = "") { }

[ByRefEvent]
public readonly record struct NewVesselEnteredMessage(
    string Name = "",
    string Description = "",
    string AmbientMusicPrototype = "") { }

[ByRefEvent]
public readonly record struct SpaceEnteredMessage { }
