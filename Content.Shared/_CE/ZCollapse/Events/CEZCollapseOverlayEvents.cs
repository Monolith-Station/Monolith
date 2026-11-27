/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Serialization;

namespace Content.Shared._CE.ZCollapse.Events;

[Serializable, NetSerializable]
public sealed class CEZCollapseOverlayToggledEvent(bool isEnabled) : EntityEventArgs
{
    public readonly bool IsEnabled = isEnabled;
}

[Serializable, NetSerializable]
public sealed class CEZCollapseOverlaySnapshotEvent(Dictionary<NetEntity, Dictionary<Vector2i, int>> grids) : EntityEventArgs
{
    public readonly Dictionary<NetEntity, Dictionary<Vector2i, int>> Grids = grids;
}
