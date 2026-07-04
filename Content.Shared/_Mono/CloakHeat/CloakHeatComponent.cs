using Content.Shared._Mono.CloakHeat.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Mono.CloakHeat;

/// <summary>
/// Component that manages heat buildup for cloaking.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(CloakHeatSystem))]
public sealed partial class CloakHeatComponent : Component
{
    /// <summary>
    /// Maximum time the Hide flag can be active before overheating.
    /// </summary>
    [DataField]
    public TimeSpan MaxCloakTime = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Duration of the cooldown period after overheating.
    /// </summary>
    [DataField]
    public TimeSpan CooldownDuration = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Current heat level, from 0.0 (cool) to 1.0 (overheated).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentHeat;

    /// <summary>
    /// When the cooldown period ends. Null if not in cooldown.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan? CooldownEndTime;

    /// <summary>
    /// Last time the heat was updated, used for calculating heat changes.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan LastUpdateTime = TimeSpan.Zero;

    /// <summary>
    /// Whether the system is currently overheated and in cooldown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsOverheated;

    /// <summary>
    /// Rate at which heat builds up when cloaking is active (per second).
    /// </summary>
    [DataField]
    public float HeatBuildupRate = 1f / 60f; // 1.0 heat over 1 minutes

    /// <summary>
    /// Rate at which heat dissipates when cloaking is inactive (per second).
    /// </summary>
    [DataField]
    public float HeatDissipationRate = 1f / 60f; // Cool in 1 minute when not cloaking
}
