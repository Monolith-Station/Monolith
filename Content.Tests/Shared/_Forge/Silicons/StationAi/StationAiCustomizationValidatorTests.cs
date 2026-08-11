using Content.Shared._Forge.Silicons.StationAi;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared._Forge.Silicons.StationAi;

[TestFixture]
[TestOf(typeof(StationAiCustomizationValidator))]
public sealed class StationAiCustomizationValidatorTests
{
    [Test]
    public void NameNormalizationUsesSharedLimitsAndRules()
    {
        Assert.Multiple(() =>
        {
            Assert.That(StationAiCustomizationValidator.TryNormalizeName("  central ai  ", true, false, out var trimmed), Is.True);
            Assert.That(trimmed, Is.EqualTo("central ai"));
            Assert.That(StationAiCustomizationValidator.TryNormalizeName("AI<script>", true, false, out _), Is.False);
            Assert.That(StationAiCustomizationValidator.TryNormalizeName(string.Empty, true, false, out _), Is.False);
            Assert.That(StationAiCustomizationValidator.TryNormalizeName(new string('A', StationAiCustomizationValidator.MaxNameLength + 1), true, false, out _), Is.False);
        });
    }

    [Test]
    public void ColorNormalizationRejectsNonFiniteAndClampsChannels()
    {
        Assert.Multiple(() =>
        {
            Assert.That(StationAiCustomizationValidator.TryNormalizeColor(new Color(float.NaN, 0f, 0f, 1f), out _), Is.False);
            Assert.That(StationAiCustomizationValidator.TryNormalizeColor(new Color(float.PositiveInfinity, 0f, 0f, 1f), out _), Is.False);
            Assert.That(StationAiCustomizationValidator.TryNormalizeColor(new Color(-1f, 0.5f, 2f, 0.25f), out var clamped), Is.True);
            Assert.That(clamped, Is.EqualTo(new Color(0f, 0.5f, 1f, 1f)));
            Assert.That(StationAiCustomizationValidator.TryNormalizeColor(Color.Black, out var black), Is.True);
            Assert.That(black, Is.EqualTo(Color.Black));
        });
    }
}
