using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Mining;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OreDrillComponent : Component
{
    [DataField] public SoundSpecifier MiningSound = new SoundPathSpecifier("/Audio/Ambience/Objects/server_fans.ogg", AudioParams.Default.WithVolume(-7));
    public EntityUid? Audio;
    [AutoNetworkedField] public ProtoId<OreDepositPrototype>? Deposit;
}
