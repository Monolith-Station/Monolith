using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Humanoid.Components;

/// <summary>
///     This is added to a marker entity in order to spawn a randomized
///     humanoid ingame.
/// </summary>
[RegisterComponent, EntityCategory("Spawner")]
public sealed partial class RandomHumanoidSpawnerComponent : Component
{
    [DataField("settings")]
    public ProtoId<RandomHumanoidSettingsPrototype>? SettingsPrototypeId;

    // Frontier: entity to use (e.g. in storage fills)
    public EntityUid SpawnedId = EntityUid.Invalid;
}
