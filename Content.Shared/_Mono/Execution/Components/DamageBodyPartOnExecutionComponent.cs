using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;

namespace Content.Shared._Mono.Execution.Components;

/// <remarks>
/// This was originally intended to simply sever said body part outright, but it has been made more versatile.
/// </remarks>
[RegisterComponent]
public sealed partial class DamageBodyPartOnExecutionComponent : Component
{
    [DataField]
    public TargetBodyPart Part = TargetBodyPart.Head;

    [DataField(required: true)]
    public DamageSpecifier Damage;

    [DataField]
    public bool IgnoreResistances = false;

    [DataField]
    public bool CanSever = true;
}