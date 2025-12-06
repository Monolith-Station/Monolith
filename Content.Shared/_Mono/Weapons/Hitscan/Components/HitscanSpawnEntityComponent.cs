using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// When this hitscan hits a target, it will create an entity defined in SpawnedEntity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanSpawnEntityComponent : Component
{
    /// <summary>
    /// Entity that will be spawned when the hitscan hits its target.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId SpawnedEntity;
};
