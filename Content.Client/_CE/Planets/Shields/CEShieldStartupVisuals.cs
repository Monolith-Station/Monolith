namespace Content.Client._CE.Planets.Shields;

internal sealed class CEShieldStartupVisuals
{
    public required Color EnergyColor { get; init; }
    public required float FlowSpeed { get; init; }
    public required float BurstSeconds { get; init; }
    public required float EndSeconds { get; init; }
    public required float BurstRadius { get; init; }
    public required float EndRadius { get; init; }

    public required float ShockwaveSeconds { get; init; }
    public required float ShockwaveRadius { get; init; }

    public required float MoteRange { get; init; }
    public required float MoteStartSpeed { get; init; }
    public required float MoteEndSpeed { get; init; }
    public required CEShieldMoteCurve MoteSpeedCurve { get; init; }
    public required float MoteSpeedVariance { get; init; }
    public required int MoteStartCount { get; init; }
    public required int MoteEndCount { get; init; }
    public required CEShieldMoteCurve MoteCountCurve { get; init; }
    public required float MoteCountRoughness { get; init; }

    public CEShieldStartupTimeline GetTimeline() =>
        CEShieldStartupTimeline.From(BurstSeconds, EndSeconds, MoteStartCount, MoteEndCount, MoteEndSpeed);
}

internal enum CEShieldMoteCurve : byte
{
    Linear,
    EaseIn,
    EaseOut,
    SmoothStep,
}
