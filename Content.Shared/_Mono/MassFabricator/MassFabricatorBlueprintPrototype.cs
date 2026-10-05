using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Mono.MassFabricator;

public enum MassFabricatorBlueprintCategory : byte
{
    Tiling,
    Bulkheads,
}

public enum MassFabricatorBlueprintKind : byte
{
    Tile,
    Entity,
}

[DataDefinition]
public sealed partial class MassFabricatorBlueprintVariant
{
    [DataField(required: true)]
    public string Name { get; private set; } = default!;

    [DataField(required: true)]
    public string Target { get; private set; } = default!;

    [DataField]
    public SpriteSpecifier? Sprite { get; private set; }

    [DataField]
    public Dictionary<string, int>? Materials { get; private set; }
}

[Prototype("massFabricatorBlueprint")]
public sealed partial class MassFabricatorBlueprintPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name { get; private set; } = default!;

    [DataField]
    public string Description { get; private set; } = string.Empty;

    [DataField]
    public MassFabricatorBlueprintCategory Category { get; private set; }

    [DataField]
    public string Subcategory { get; private set; } = string.Empty;

    [DataField]
    public MassFabricatorBlueprintKind Kind { get; private set; }

    [DataField]
    public string? Target { get; private set; }

    [DataField]
    public SpriteSpecifier? Sprite { get; private set; }

    [DataField]
    public List<MassFabricatorBlueprintVariant> Variants { get; private set; } = new();

    public bool HasTarget(string? target) => target != null &&
        (Variants.Count == 0 ? target == Target : Variants.Exists(variant => variant.Target == target));

    [DataField(required: true)]
    public int Load { get; private set; }

    [DataField(required: true)]
    public Dictionary<string, int> Materials { get; private set; } = new();

    public Dictionary<string, int> GetMaterials(string target) =>
        Variants.Find(variant => variant.Target == target)?.Materials ?? Materials;

    public float PrintDuration(int maxLoad)
    {
        var fraction = (float) Load / Math.Max(maxLoad, 1);
        return Kind == MassFabricatorBlueprintKind.Tile
            ? 0.4f + fraction * 20f : 1.5f + fraction * 80f;
    }
}
