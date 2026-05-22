using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Glory.Biome;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GridBiomeComponent : Component
{
    [DataField]
    public ProtoId<GridBiomePrototype> BiomePrototype = "None";
}
