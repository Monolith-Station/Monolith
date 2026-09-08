using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Planets;

[RegisterComponent]
public sealed partial class SuppressedTileSpawnsComponent : Component
{
    [DataField]
    public HashSet<EntProtoId> Prototypes = new();
}
