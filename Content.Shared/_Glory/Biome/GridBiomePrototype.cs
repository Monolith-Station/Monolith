using Robust.Shared.Prototypes;

namespace Content.Shared._Glory.Biome;

/// <summary>
/// This is a prototype for...
/// </summary>
[Prototype]
public sealed partial class GridBiomePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string? StateName = "";
}
