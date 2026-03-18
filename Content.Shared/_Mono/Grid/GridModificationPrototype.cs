using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Grid;

/// <summary>
/// This prototypes stores all grid modifiers to process them.
/// </summary>
[Prototype("gridModifier")]
public sealed class GridModificationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField]
    public List<GridModifier> Modifiers = [];
}
