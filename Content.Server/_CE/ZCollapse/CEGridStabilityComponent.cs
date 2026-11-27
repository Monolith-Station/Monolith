/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Map;

namespace Content.Server._CE.ZCollapse;

[RegisterComponent]
public sealed partial class CEGridStabilityComponent : Component
{
    [ViewVariables]
    public readonly Dictionary<Vector2i, int> Stability = new();

    [ViewVariables]
    public readonly HashSet<EntityUid> Cores = new();

    [ViewVariables]
    public readonly HashSet<EntityUid> Supports = new();

    [ViewVariables]
    public readonly Dictionary<Vector2i, TimeSpan> PendingCollapses = new();

    [DataField]
    public bool SupportLowestLevel;
}
