using Robust.Shared.Serialization;

namespace Content.Client._Crescent.SpaceBiomes;

[Serializable]
public sealed class SpaceBiomeSwapMessage : EntityEventArgs
{
    public string Biome = "";
}

[Serializable]
public sealed class NewVesselEnteredMessage : EntityEventArgs
{
    public string Name = "";
    public string Description = "";
    public string AmbientMusicPrototype = "";

    public NewVesselEnteredMessage(string name, string description, string ambientMusicPrototype)
    {
        Name = name;
        Description = description;
        AmbientMusicPrototype = ambientMusicPrototype;
    }
}

[Serializable]
public sealed class SpaceEnteredMessage : EntityEventArgs
{
}
