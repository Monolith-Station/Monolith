using Content.Shared._NF.Cloning;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgerySpeedModifierComponent : Component, ITransferredByCloning // Mono: transferred by cloning
{
    [DataField]
    public float SpeedModifier = 1.5f;
}