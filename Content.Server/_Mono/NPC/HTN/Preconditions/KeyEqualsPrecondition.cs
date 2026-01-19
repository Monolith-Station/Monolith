using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server._Mono.NPC.HTN.Preconditions;

/// <summary>
/// Checks for the presence of the value by the specified <see cref="KeyEqualsPrecondition.Key"/> in the <see cref="NPCBlackboard"/>.
/// Returns true if it equals value set.
/// </summary>
public sealed partial class KeyEqualsPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string Key = string.Empty;

    [DataField(required: true)]
    public string Value = string.Empty;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue<string>(Key, out var value, _entManager) && value.Equals(Value);
    }
}
