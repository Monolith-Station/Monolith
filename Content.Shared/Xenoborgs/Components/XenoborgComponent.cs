using Robust.Shared.Audio;

namespace Content.Shared.Xenoborgs.Components;

/// <summary>
/// Defines what is a xenoborg for the intentions of the xenoborg rule.
/// If all xenoborg cores are destroyed, all xenoborgs will self-destruct.
///
/// It's also used by the mothership core.
/// </summary>
[RegisterComponent]
public sealed partial class XenoborgComponent : Component
{

    /// <summary>
    /// The text that is sent when you become a xenoborg.
    /// </summary>
    [DataField]
    public LocId BriefingText = "xenoborgs-welcome";

    /// <summary>
    /// Briefing sound when you become a xenoborg.
    /// </summary>
    [DataField]
    public SoundSpecifier BriefingSound = new SoundPathSpecifier("/Audio/Ambience/Antag/xenoborg_start.ogg");
}