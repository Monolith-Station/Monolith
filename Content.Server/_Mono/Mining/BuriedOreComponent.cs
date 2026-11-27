using System.Numerics;
using Content.Shared._Mono.Mining;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Mining;

[RegisterComponent]
public sealed partial class BuriedOreComponent : Component
{
    [DataField] public PlanetOreDeposits Configuration = new();
    [DataField, ViewVariables] public Dictionary<Vector2i, BuriedOreVein?> Cells { get; private set; } = new();
    [ViewVariables] public int CellCount => Cells.Count;
}

[DataDefinition]
public sealed partial class BuriedOreVein
{
    [DataField] public ProtoId<OreDepositPrototype> Mineral;
    [DataField, ViewVariables] public Vector2i Cell;
    [DataField] public Vector2[] Boundary = Array.Empty<Vector2>();
    [DataField] public int Total;
    [DataField] public int Remaining;
    [DataField] public float InitialSaturation;
    [DataField] public float BaseRate;
    [DataField] public float Remainder;
    [DataField] public int NextDrill;

    public float Saturation => Total > 0 ? InitialSaturation * MathF.Sqrt((float)Remaining / Total) : 0f;
    public float Rate => BaseRate * Saturation;
}
