/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    private void InitializeCacheHooks()
    {
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);
    }

    private void OnGridRemoved(GridRemovalEvent ev)
    {
        _previousGridPosition.Remove(ev.EntityUid);
        _wallsNearGrid.Remove(ev.EntityUid);
    }
}
