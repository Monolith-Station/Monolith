using System.Threading;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared.Nyanotrasen.Digging;

[RegisterComponent]
public sealed partial class EarthDiggingComponent : Component
{
    [ViewVariables]
    [DataField("toolComponentNeeded")]
    public bool ToolComponentNeeded = true;

    [ViewVariables]
    [DataField]
    public ProtoId<ToolQualityPrototype> QualityNeeded = "Digging";

    [ViewVariables]
    [DataField("delay")]
    public float Delay = 2f;

}
