using Content.Shared.Construction;
using Content.Shared.Construction.Steps;
using Content.Shared.DoAfter;

namespace Content.Server.Construction;

[ByRefEvent]
public record struct GetConstructionToolUseDurationEvent(
    EntityUid User,
    EntityUid Tool,
    ConstructionGraphEdge Edge,
    ToolConstructionGraphStep Step,
    TimeSpan Duration);

public sealed class ConstructionToolUseStartedEvent(
    EntityUid user,
    EntityUid tool,
    ConstructionGraphEdge edge,
    ToolConstructionGraphStep step,
    DoAfterId doAfter) : EntityEventArgs
{
    public EntityUid User { get; } = user;
    public EntityUid Tool { get; } = tool;
    public ConstructionGraphEdge Edge { get; } = edge;
    public ToolConstructionGraphStep Step { get; } = step;
    public DoAfterId DoAfter { get; } = doAfter;
}
