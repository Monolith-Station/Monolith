using Content.Shared.DoAfter;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Mono.Weapons.Ranged.Heat;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class HeatAmmoProviderComponent : AmmoProviderComponent
{
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId Proto;

    [DataField, AutoNetworkedField]
    public float MaxHeat = 20f;

    [DataField, AutoNetworkedField]
    public float HeatPerShot = 1f;

    [DataField, AutoNetworkedField]
    public float HeatDissipation = 0.5f;

    [DataField, AutoNetworkedField]
    public float HeatDissipationFactor = 0f;

    [DataField, AutoNetworkedField]
    public float MaxHeatDamage = 10f;

    [DataField, AutoNetworkedField]
    public Color LowHeatColor = Color.Yellow;

    [DataField, AutoNetworkedField]
    public Color HighHeatColor = Color.Red;

    [DataField, AutoNetworkedField]
    public Color BackgroundColor = Color.FromHex("#292929");

    [DataField]
    public TimeSpan VentDuration = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan VentCooldown = TimeSpan.FromSeconds(15);

    [DataField]
    public SoundSpecifier? VentSound;

    [DataField, AutoNetworkedField]
    public float Heat;

    [DataField, AutoNetworkedField]
    public bool Overheated;

    [AutoNetworkedField]
    public bool Venting;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextVent;
}

[Serializable, NetSerializable]
public sealed partial class HeatVentDoAfterEvent : SimpleDoAfterEvent;
