using Content.Server.NPC.Queries.Considerations;
using Content.Server.NPC.Queries.Queries;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array; // Mono

namespace Content.Server.NPC.Queries;

/// <summary>
/// Stores data for generic queries.
/// Each query is run in turn to get the final available results.
/// These results are then run through the considerations.
/// </summary>
[Prototype]
public sealed partial class UtilityQueryPrototype : IPrototype, IInheritingPrototype // Mono
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // <Mono>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<UtilityQueryPrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc />
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }
    // </Mono>

    [ViewVariables(VVAccess.ReadWrite), DataField("query"), AlwaysPushInheritance]
    public List<UtilityQuery> Query = new();

    [ViewVariables(VVAccess.ReadWrite), DataField("considerations")]
    public List<UtilityConsideration> Considerations = new();

    /// <summary>
    /// How many entities we are allowed to consider. This is applied after all queries have run.
    /// </summary>
    [DataField("limit")]
    public int Limit = 128;
}
