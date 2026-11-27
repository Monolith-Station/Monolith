using Content.Shared._Mono.Planets;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Planets;

[RegisterComponent]
public sealed partial class PlanetMapGenerationComponent : Component
{
    [DataField] public ProtoId<PlanetMapPrototype> Definition;
    [DataField] public int Seed;
    [DataField] public Dictionary<int, EntityUid> Layers = new();
}
