namespace Content.Server._Mono.Cloning;

[RegisterComponent]
public sealed partial class GeneticDatabaseClientComponent : Component
{
    [ViewVariables]
    public EntityUid? Database;
}
