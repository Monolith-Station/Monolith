using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Sensors;

[RegisterComponent, NetworkedComponent]
public sealed partial class ConsoleRadarLockToneComponent : Component
{
    [DataField]
    public SoundSpecifier LockAcquiredSound = new SoundPathSpecifier("/Audio/_Mono/Effects/Alerts/rwr_beep.ogg");

    [DataField]
    public SoundSpecifier MissileTone = new SoundPathSpecifier("/Audio/_Mono/Effects/Alerts/an_alr63_missile.ogg");

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(3);
}
