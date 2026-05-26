using System;

namespace Content.Server.RoundEnd;

/// <summary>
/// If a gamerule with this component is present, override the roundend time to the time set in it.
/// </summary>
[RegisterComponent]
public sealed partial class RoundEndTimeRuleComponent : Component
{
    [DataField]
    public TimeSpan EndAt;
}
