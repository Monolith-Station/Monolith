using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Crescent.SpaceBiomes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpaceBiomeSourceComponent : Component
{
    [AutoNetworkedField]
    [DataField(required: true)]
    public ProtoId<SpaceBiomePrototype> Id;

    /// <summary>
    /// Distance at which swap should begin
    /// null = infinite distance
    /// </summary>
    [AutoNetworkedField]
    [DataField(required: true)]
    public float? SwapDistance;


    /// <summary>
    /// If multiple biomes are overlapping, biome with the highest priority is applied
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public float Priority;
}
