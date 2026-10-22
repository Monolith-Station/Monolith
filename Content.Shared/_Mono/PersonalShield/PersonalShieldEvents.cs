using Content.Shared.Actions;

namespace Content.Shared._Mono.PersonalShield;

public sealed partial class PersonalShieldActionEvent : InstantActionEvent;

[ByRefEvent]
public record struct GetPersonalShieldStatsEvent
{
    public float MaxCharge;
    public float RegenRate;
    public float SpinupTime;
    public float BreakCooldown;
    public float PowerDraw;

    public GetPersonalShieldStatsEvent(PersonalShieldSettings settings)
    {
        MaxCharge = settings.MaxCharge;
        RegenRate = settings.RegenRate;
        SpinupTime = settings.SpinupTime;
        BreakCooldown = settings.BreakCooldown;
        PowerDraw = settings.PowerDraw;
    }
}
