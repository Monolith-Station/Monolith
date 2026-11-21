using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Mono.Ships;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ShipShieldProjectorComponent : Component
{
    public const string FixturePrefix = "monoShieldShell-";
    public const float ImpactDuration = 1f;
    public const float ContactImpactPower = 0.65f;
    public const float MaxEmpStallSeconds = 30f;
    public static readonly TimeSpan ImpactHistoryLifetime = TimeSpan.FromSeconds(1.5);
    public static readonly TimeSpan PressureGrace = TimeSpan.FromSeconds(0.12);

    [DataField, AutoNetworkedField]
    public ShipShieldShellSettings Shield = new();

    [DataField, AutoNetworkedField]
    public ShipShieldShellVisuals Visuals = new();

    [DataField, AutoNetworkedField]
    public ShipShieldShellState State;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;

    [AutoNetworkedField] public List<ShipShieldImpact> Impacts = new();
    [AutoNetworkedField] public Dictionary<NetEntity, ShipShieldBodyContact> BodyContacts = new();
    public uint NextImpactId;

    [DataField, AutoNetworkedField] public float MaxCapacity = 65_000f;
    [DataField] public float BaseDraw = 20_000f;
    [DataField] public float RechargeDraw = 40_000f;
    [DataField] public float RechargeRate = 500f;
    [DataField, AutoNetworkedField] public float BreakRespoolTime = 5f;
    [DataField] public float CrashRespoolTime = 30f;
    [DataField] public float EmpDamagePerJoule = 0.01f;
    [DataField] public float EmpStallSecondsPerMegajoule = 10f;
    [DataField, AutoNetworkedField] public float ImpactDamageScale = 0.001f;
    [DataField, AutoNetworkedField] public DamageModifierSet DamageModifiers = new()
    {
        Coefficients = { ["Structural"] = 10f },
    };

    [DataField, AutoNetworkedField] public float Damage;
    [AutoNetworkedField] public float PoweredRechargeRate;
    [AutoNetworkedField] public bool HasOperatingPower;
    [AutoNetworkedField] public bool StartupPowerAvailable;
    [AutoNetworkedField, AutoPausedField] public TimeSpan? StartupSpoolAt;
    [AutoNetworkedField, AutoPausedField] public TimeSpan RespoolUntil;
    [AutoNetworkedField, AutoPausedField] public TimeSpan RechargeStalledUntil;

    public float Capacity => Math.Clamp(MaxCapacity - Damage, 0f, MaxCapacity);
    public bool RequestsPower => State is ShipShieldShellState.Starting or ShipShieldShellState.Active
        or ShipShieldShellState.Brownout or ShipShieldShellState.Standby or ShipShieldShellState.Shattering;
    public bool IsShattering => State is ShipShieldShellState.Shattering or ShipShieldShellState.ShatteringOff;

    public bool CanRecharge(TimeSpan now) =>
        State == ShipShieldShellState.Active && AnimationPhase(now) >= 1f &&
        now >= RechargeStalledUntil && Capacity < MaxCapacity;

    public float GetEmpDamage(float energy) => MathF.Max(0f, energy * EmpDamagePerJoule);

    public TimeSpan AnimationDuration(ShipShieldShellState state)
    {
        var seconds = state switch
        {
            ShipShieldShellState.Starting or ShipShieldShellState.Active => Shield.SpinupTime,
            ShipShieldShellState.PoweringDown or ShipShieldShellState.Brownout => Shield.ShutdownTime,
            ShipShieldShellState.Shattering or ShipShieldShellState.ShatteringOff => Visuals.ShatterTime,
            _ => 0f,
        };

        return TimeSpan.FromSeconds(MathF.Max(seconds, 0.01f));
    }

    public float AnimationPhase(TimeSpan now)
    {
        if (State == ShipShieldShellState.Starting)
            return StartupSpoolAt is { } start
                ? Math.Clamp((float) ((now - start) / AnimationDuration(State)), 0f, 0.85f)
                : 0f;

        var progress = Math.Clamp((float) ((now - StartedAt) / AnimationDuration(State)), 0f, 1f);
        return State switch
        {
            ShipShieldShellState.Active => progress,
            ShipShieldShellState.PoweringDown or ShipShieldShellState.Brownout => 1f - progress,
            ShipShieldShellState.Shattering or ShipShieldShellState.ShatteringOff => 1f + progress,
            _ => 0f,
        };
    }
}

[Serializable, NetSerializable]
public readonly record struct ShipShieldImpact(uint Id, Vector3 Origin, MapId Map, TimeSpan At, float Power, NetEntity? Source);

[Serializable, NetSerializable]
public readonly record struct ShipShieldBodyContact(TimeSpan PressureUntil, TimeSpan PassiveUntil);

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ShipShieldShellSettings
{
    // XY radii are in tiles. Z radius and center height are in decks.
    [DataField] public Vector3 Radii = new(6f, 6f, 1.5f);
    [DataField] public float CenterHeight;
    [DataField] public float SpinupTime = 0.75f;
    [DataField] public float ShutdownTime = 2.5f;
    [DataField] public ShipShieldCollision CollisionMask = ShipShieldCollision.All;

    public bool Blocks(ShipShieldCollision category) => (CollisionMask & category) != ShipShieldCollision.None;
}

[Flags, Serializable, NetSerializable]
public enum ShipShieldCollision : byte
{
    None = 0,
    Mobs = 1 << 0,
    Objects = 1 << 1,
    Grids = 1 << 2,
    Shields = 1 << 3,
    All = Mobs | Objects | Grids | Shields,
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ShipShieldShellVisuals
{
    [DataField] public Color Color = Color.FromHex("#75DFFF");
    // Generator-local XY offset in tiles.
    [DataField] public Vector2 SpoolOffset;
    [DataField] public float ShatterTime = 1f;
    [DataField] public float InteriorFadeTime = 0.25f;
}

public enum ShipShieldShellState : byte
{
    Off,
    Active,
    PoweringDown,
    Shattering,
    Starting,
    Standby,
    Brownout,
    ShatteringOff,
}
