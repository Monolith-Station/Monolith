namespace Content.Server._Mono.AlertLevel;

/// <summary>
/// Way of indicating if a round is before or after war declaration when you join.
/// </summary>
[RegisterComponent]
public sealed partial class WarLevelComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)] public bool PostWar = false;
}
