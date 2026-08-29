using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlanetBodyComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid StarSystemMap;

    [DataField, AutoNetworkedField]
    public ProtoId<PlanetTypePrototype> Type;

    [DataField, AutoNetworkedField]
    public int Index;

    [DataField, AutoNetworkedField]
    public EntityUid? SurfaceNetwork;

    [DataField, AutoNetworkedField]
    public float Radius;

    public float ApproachRadius => Radius * Planet.MAP_PIXEL_SIZE;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlanetSurfaceComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Planet;

    [DataField, AutoNetworkedField]
    public EntityUid SpaceMap;
}
