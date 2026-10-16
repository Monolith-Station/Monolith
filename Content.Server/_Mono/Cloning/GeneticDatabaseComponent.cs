namespace Content.Server._Mono.Cloning;

[RegisterComponent]
public sealed partial class GeneticDatabaseComponent : Component
{
    [DataField]
    public string DatabaseName = "GENEDB";

    [ViewVariables]
    public Dictionary<EntityUid, GeneticRecord> Records = new();

    [ViewVariables]
    public int NextId = 1;
}
