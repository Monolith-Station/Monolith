using System.Numerics;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.StarSystem;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class StarSystemMapComponent : Component
{
    [ViewVariables] public PlanetarySystem? StarSystem;
    [ViewVariables, AutoNetworkedField] public Vector2 StarOffset;
}
