using Content.Shared._Mono.Claws.ClawTypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Claws;

/// <summary>
/// This is a prototype claws with growth logic and claw type
/// </summary>
[Prototype]
public sealed partial class ClawPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public TimeSpan GrowCooldown = TimeSpan.FromSeconds(1200);

    [DataField]
    public bool CanGrow = true;

    [DataField(required: true)]
    public ClawType ClawType = default!;
}
