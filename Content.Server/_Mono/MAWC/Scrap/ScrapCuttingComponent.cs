using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.MAWC.Scrap;

/// <summary>
/// HULLROT Part 2: Somehow, The Scrap Cutter Returned
/// </summary>
[RegisterComponent]
public sealed partial class ScrapCuttingComponent : Component
{
    [DataField]
    public EntProtoId Scrap = "MaterialMAWCScrapHot1";

    [DataField]
    public List<ScrapYield> Yields = new();

    [DataField]
    public int DefaultYield = 2;

    [DataField]
    public EntityWhitelist? Blacklist;
}

[DataDefinition]
public sealed partial class ScrapYield
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public int Min = 1;

    [DataField]
    public int Max = 1;
}
