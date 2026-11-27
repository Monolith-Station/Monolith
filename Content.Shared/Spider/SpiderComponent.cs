using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Spider;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSpiderSystem))]
public sealed partial class SpiderComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public EntProtoId WebPrototype = "SpiderWeb";

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public EntProtoId WebAction = "ActionSpiderWeb";

    [DataField] public EntityUid? Action;
}

public sealed partial class SpiderWebActionEvent : InstantActionEvent { }
