using Content.Shared._Crescent.SpaceBiomes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Shuttles.Components;

/// <summary>
/// Marker that specifies a map as being for FTLing entities.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FTLMapComponent : Component
{
    /// <summary>
    /// Offset for FTLing shuttles so they don't overlap each other.
    /// </summary>
    [DataField]
    public int Index;

    /// <summary>
    /// What parallax to use for the background, immediately gets deffered to ParallaxComponent.
    /// </summary>
    [DataField]
    public string Parallax = "FastSpace";

    /// <summary>
    /// Can FTL on this map only be done to beacons.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Beacons;

    /// <summary>
    /// Mono: used for ContentAudioSystem.AmbientMusic.cs & SpaceBiomeSystem.cs to communicate biome on FTL
    /// </summary>
    [DataField]
    public ProtoId<SpaceBiomePrototype> Biome = "BiomeFTL";
}
