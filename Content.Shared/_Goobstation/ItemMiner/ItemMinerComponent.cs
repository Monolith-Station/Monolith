using Content.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using System;

namespace Content.Shared._Goobstation.ItemMiner;

/// <summary>
/// Periodically passively produces entities, possibly with conditions.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ItemMinerComponent : Component
{
    /// <summary>
    /// Time for next item to be generated at.
    /// </summary>
    [DataField, AutoPausedField]
    public TimeSpan NextAt;

    /// <summary>
    /// Prototype to spawn.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Proto;

    /// <summary>
    /// Sound to loop while working.
    /// </summary>
    [DataField]
    public SoundSpecifier? MiningSound = new SoundPathSpecifier("/Audio/Ambience/Objects/server_fans.ogg", AudioParams.Default.WithVolume(-7));

    /// <summary>
    /// Sound to play when printing an item.
    /// </summary>
    [DataField]
    public SoundSpecifier? MinedSound = null;

    /// <summary>
    /// How often to produce the item.
    /// </summary>
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(10.0f);

    /// <summary>
    /// Whether to need to be anchored to run.
    /// </summary>
    [DataField]
    public bool NeedsAnchored = true;

    /// <summary>
    /// Whether to need APC power to run.
    /// </summary>
    [DataField]
    public bool NeedApcPower = true;

    [ViewVariables]
    public EntityUid? AudioUid = null;

    // if you want to add a planetary miner or other varieties of miner, don't add more stuff to this, make a new comp and use events
}
