using Content.Shared.Explosion;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Radio;

[Prototype]
public sealed partial class HeadsetPunishmentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<string> Words = new();

    [DataField]
    public ProtoId<ExplosionPrototype> Explosion = "HeadsetPunishment";

    [DataField]
    public float BaseIntensity = 8f;

    [DataField]
    public float IntensityPerMatch = 8f;

    [DataField]
    public float MaxIntensity = 40f;

    [DataField]
    public float StunSecondsPerMatch = 2f;
}
