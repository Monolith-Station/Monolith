/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Server._CE.ZCollapse;

[RegisterComponent]
public sealed partial class CEGridStabilityCoreComponent : Component
{
    [DataField]
    public int LevitationForce = 20;
}
