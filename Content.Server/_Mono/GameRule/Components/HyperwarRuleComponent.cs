namespace Content.Server._Mono.GameRule.Components;

[RegisterComponent]
public sealed partial class HyperwarRuleComponent : Component
{
    [DataField]
    public float LatheMaterialUseMultiplier = 0.25f;

    [DataField]
    public float LatheTimeMultiplier = 0.1f;
}
