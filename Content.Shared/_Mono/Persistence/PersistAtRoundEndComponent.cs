namespace Content.Shared._Mono.Persistence;

[RegisterComponent]
public sealed partial class PersistAtRoundEndComponent : Component
{
    [DataField]
    public bool Sticky;

    [DataField]
    public bool Once;
}
