using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class StarSystemMapComponent : Component
{
    [DataField, ViewVariables, AutoNetworkedField] public ProtoId<StarSystemPrototype>? System;
    [ViewVariables] public PlanetarySystem? StarSystem;
}
