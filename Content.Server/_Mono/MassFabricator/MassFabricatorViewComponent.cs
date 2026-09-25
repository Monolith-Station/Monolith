namespace Content.Server._Mono.MassFabricator;

[RegisterComponent]
public sealed partial class MassFabricatorViewComponent : Component
{
    public EntityUid Eye;
    public EntityUid Fabricator;
    public bool Returning;
    public uint ReturnStartTick;
}
