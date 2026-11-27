/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._CE.ZLevels.Roof;

namespace Content.Server._CE.ZLevels.Roof;

public sealed partial class CEZLevelsRoofSystem : CESharedZLevelsRoofSystem
{
    [Dependency] private CESharedZLevelsSystem _levels = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitMaps();
    }
}
