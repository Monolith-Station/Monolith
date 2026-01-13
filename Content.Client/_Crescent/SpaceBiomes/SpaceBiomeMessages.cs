using Content.Shared._Crescent.SpaceBiomes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Client._Crescent.SpaceBiomes;

/// <summary>
/// used by space biome system to signal when the player has changed biomes, to check for biome music
/// </summary>
/// <param name="Id"></param>
[ByRefEvent]
public readonly record struct SpaceBiomeSwapMessage(ProtoId<SpaceBiomePrototype> Id);

/// <summary>
/// used by space biome system to signal when the player's parent is changed, to check for grid music
/// </summary>
/// <param name="Grid"></param>
[ByRefEvent]
public readonly record struct PlayerParentChangedMessage(EntityUid? Grid); //null = space

/// <summary>
/// used by space biome system to add biome components to dynamically-created maps, like FTLmap and expeditions
/// </summary>
/// <param name="Id"></param>
[ByRefEvent]
public record struct SpaceBiomeMapChangeMessage(ProtoId<SpaceBiomePrototype>? Biome);
