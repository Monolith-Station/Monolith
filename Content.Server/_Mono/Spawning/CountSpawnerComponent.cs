using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Spawning;

/// <summary>
/// Simple spawner that is made to use SpawnCount system
/// Lets to spawn entities with randomized stack counts.
/// </summary>
[RegisterComponent]
public sealed partial class CountSpawnerComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Prototype = string.Empty;

    [DataField]
    public int MinimumCount = 1;

    [DataField]
    public int MaximumCount = 1;

    [DataField]
    public bool DespawnAfterSpawn = true;
}
