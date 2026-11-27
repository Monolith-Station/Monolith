using Robust.Shared.GameStates;

namespace Content.Shared._Mono.MassFabricator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MassFabricatorViewComponent : Component
{
    [AutoNetworkedField]
    public EntityUid Eye;

    [AutoNetworkedField]
    public EntityUid Fabricator;

    [AutoNetworkedField]
    public bool Returning;

    [AutoNetworkedField]
    public uint ReturnStartTick;
}
