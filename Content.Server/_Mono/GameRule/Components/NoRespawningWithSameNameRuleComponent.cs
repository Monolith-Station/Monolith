using Robust.Shared.Network;

namespace Content.Server._Mono.GameRule.Components;

[RegisterComponent]
public sealed partial class NoRespawningWithSameNameRuleComponent : Component
{
    [DataField]
    public Dictionary<NetUserId, List<string>> SpawnedCharacters = new Dictionary<NetUserId, List<string>>();
}