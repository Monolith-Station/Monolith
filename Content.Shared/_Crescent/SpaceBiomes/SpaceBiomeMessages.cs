using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.SpaceBiomes;

[Serializable, NetSerializable]
public sealed class SpaceBiomeSwapMessage : EntityEventArgs
{
    public string Biome = "";
}

[Serializable, NetSerializable]
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
