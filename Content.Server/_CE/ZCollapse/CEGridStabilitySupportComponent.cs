/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Server._CE.ZCollapse;

[RegisterComponent]
public sealed partial class CEGridStabilitySupportComponent : Component
{
    [DataField]
    public int SupportStrength = 7;

    [DataField]
    public int TransferLoss;
}
