using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Mono.MAWC.Scrap;

/// <summary>
/// ow my hands
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class HotScrapComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan CoolTime = TimeSpan.FromMinutes(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan CooledAt;

    [DataField]
    public DamageSpecifier Burn = new();

    [DataField]
    public TimeSpan BurnInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextBurn;
}

[Serializable, NetSerializable]
public enum HotScrapVisualLayers : byte
{
    Warm,
}
