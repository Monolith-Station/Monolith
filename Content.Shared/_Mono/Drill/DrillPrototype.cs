using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Drill;

/// <summary>
/// This is a prototype for...
/// </summary>
[Prototype()]
public sealed partial class DrillPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set;  } = default!;

    [DataField]
    public DrillType DrillType { get; private set; } = default!;
}
