using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests.Localization;

[TestFixture]
public sealed class CapibaraCultureTest
{
    [Test]
    public async Task ActiveCultureIsSpanishWithEnglishFallback()
    {
        await using var pair = await PoolManager.GetServerClient();
        var loc = pair.Server.ResolveDependency<ILocalizationManager>();

        // 1. Active culture switched to es-ES.
        Assert.That(loc.DefaultCulture?.Name, Is.EqualTo("es-ES"),
            "Active culture should be es-ES after the Capibara switch.");

        // 2. A Spanish key resolves to its Spanish value.
        Assert.That(loc.GetString("capibara-loc-smoke"),
            Is.EqualTo("Prueba de localización de Capibara"),
            "es-ES seed key should resolve from the es-ES tree.");

        // 3. An en-US-only key still resolves via fallback (not the raw id).
        //    zzzz-fmt-playtime lives in en-US/_lib.ftl and is not in the es-ES seed.
        Assert.That(loc.HasString("zzzz-fmt-playtime"), Is.True,
            "en-US fallback should resolve keys missing from es-ES.");

        await pair.CleanReturnAsync();
    }
}
