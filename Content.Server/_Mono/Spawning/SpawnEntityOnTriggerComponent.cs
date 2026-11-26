using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Spawning;

[RegisterComponent]
public sealed partial class SpawnEntityOnTriggerComponent : Component
{
    /// <summary>
    /// The prototype of the entity to spawn when triggered.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype;
}