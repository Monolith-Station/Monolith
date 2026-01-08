using Robust.Shared.Serialization;

namespace Content.Client._Crescent.SpaceBiomes;

[ByRefEvent]
public readonly record struct SpaceBiomeSwapMessage
{
    public readonly string Biome = "";
    public SpaceBiomeSwapMessage(string biome)
    {
        Biome = biome;
    }
}

[ByRefEvent]
public readonly record struct NewVesselEnteredMessage
{
    public readonly string Name = "";
    public readonly string Description = "";
    public readonly string AmbientMusicPrototype = "";

    public NewVesselEnteredMessage(string name, string description, string ambientMusicPrototype)
    {
        Name = name;
        Description = description;
        AmbientMusicPrototype = ambientMusicPrototype;
    }
}

[ByRefEvent]
public readonly record struct SpaceEnteredMessage
{
}
