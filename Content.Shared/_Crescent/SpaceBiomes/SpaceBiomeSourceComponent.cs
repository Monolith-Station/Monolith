using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Crescent.SpaceBiomes;

[RegisterComponent]
public sealed partial class SpaceBiomeSourceComponent : Component
{
    [DataField(required: true)]
    public ProtoId<SpaceBiomePrototype> Id;

    /// <summary>
    /// Distance at which swap should begin
    /// Since system is updated once in several seconds it may happen significantly later, so set this to atleast 100-150m
    /// </summary>
    [DataField(required: true)]
    public float? SwapDistance; // if null - infinite swap distance

    /// <summary>
    /// If multiple biomes are overlapping, biome with the highest priority is applied
    /// </summary>
    [DataField]
    public float Priority;
}
