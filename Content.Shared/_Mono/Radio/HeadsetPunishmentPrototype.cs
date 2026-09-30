using System.Text;
using Content.Shared.Explosion;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Radio;

[Prototype]
public sealed partial class HeadsetPunishmentPrototype : IPrototype, ISerializationHooks
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("words")]
    private List<string> _plainWords = new();

    [DataField("wordsBase64")]
    private List<string> _encodedWords = new();

    public List<string> Words { get; private set; } = new();

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

    void ISerializationHooks.AfterDeserialization()
    {
        var words = new HashSet<string>(_plainWords, StringComparer.OrdinalIgnoreCase);
        foreach (var encoded in _encodedWords)
        {
            var word = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            if (!string.IsNullOrWhiteSpace(word))
                words.Add(word);
        }

        Words = new List<string>(words);
    }
}
