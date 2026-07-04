using Robust.Shared.Audio;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System;

namespace Content.Server._Mono.ScuttleDevice;

/// <summary>
///     Altered copy of NukeComponent used to scuttle ships.
///     Requires a specific entity to unlock, after which, it can be detonated and disarmed like a normal nuke.
/// </summary>
[RegisterComponent]
public sealed partial class ScuttleDeviceComponent : Component
{
    /// <summary>
    ///     Default bomb timer value.
    /// </summary>
    [DataField]
    public TimeSpan Timer = TimeSpan.FromSeconds(300);

    /// <summary>
    ///     If the nuke is disarmed, this sets the minimum amount of time the timer can have.
    ///     The remaining time will reset to this value if it is below it.
    /// </summary>
    [DataField]
    public TimeSpan MinimumTime = TimeSpan.FromSeconds(180);

    /// <summary>
    ///     How long until the bomb can arm again after deactivation.
    ///     Used to prevent announcements spam.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     When this time is left, nuke will play last alert sound
    /// </summary>
    [DataField]
    public TimeSpan AlertSoundTime = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     How long a user must wait to arm the bomb.
    /// </summary>
    [DataField]
    public TimeSpan ArmDoafterLength = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     How long a user must wait to disarm the bomb.
    /// </summary>
    [DataField]
    public TimeSpan DisarmDoafterLength = TimeSpan.FromSeconds(30);

    [DataField]
    public SoundSpecifier ActivateSound = new SoundPathSpecifier("/Audio/Misc/delta.ogg");

    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Machines/Nuke/nuke_alarm.ogg");

    [DataField]
    public SoundSpecifier ArmSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");

    [DataField]
    public SoundSpecifier DisarmSound = new SoundPathSpecifier("/Audio/Misc/notice2.ogg");

    [DataField]
    public SoundSpecifier ArmMusic = new SoundCollectionSpecifier("NukeMusic");

    /// <summary>
    ///     How much price to add if we're unlocked.
    /// </summary>
    [DataField]
    public float UnlockedPrice = 0f;

    [DataField]
    public float AnnounceRadius = 1500f;

    [DataField]
    public bool DisarmOnUnanchor = false;

    /// <summary>
    ///     Disarm the nuke if it changes map by, say, going to FTL.
    /// </summary>
    [DataField]
    public bool DisarmOnMapChange = false;

    [DataField]
    public bool DoMusic = true;

    [DataField]
    public LocId AnnounceSender = "scuttle-device-announcement-sender";

    [ViewVariables]
    public MapId ArmedMap = MapId.Nullspace;

    /// <summary>
    ///     Time until explosion in seconds.
    /// </summary>
    [ViewVariables]
    public TimeSpan RemainingTime;

    /// <summary>
    ///     Time until bomb cooldown will expire in seconds.
    /// </summary>
    [ViewVariables]
    public TimeSpan? CooldownTime = null;

    [ViewVariables]
    public bool Armed = false;

    /// <summary>
    ///     Check if nuke has already played the nuke song so we don't do it again
    /// </summary>
    public bool PlayedNukeSong = false;

    public TimeSpan NukeSongLength;

    public ResolvedSoundSpecifier SelectedNukeSong = String.Empty;

    /// <summary>
    ///     Check if nuke has already played last alert sound
    /// </summary>
    public bool PlayedAlertSound = false;

    public EntityUid? AlertAudioStream = default;
}
