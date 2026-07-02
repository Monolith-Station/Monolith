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

        // 4. Content-registered Fluent functions work inside the es-ES bundle.
        //    Regression: MANY/MAKEPLURAL were en-US-only, so es-ES messages using
        //    them logged "Unknown function: MANY()" at runtime.
        Assert.That(loc.GetString("capibara-loc-many", ("count", 1)),
            Is.EqualTo("segundo"),
            "MANY() should return the singular form for count 1 in es-ES.");
        Assert.That(loc.GetString("capibara-loc-many", ("count", 2)),
            Is.EqualTo("segundos"),
            "MANY() should pluralize with Spanish rules in es-ES.");

        await pair.CleanReturnAsync();
    }
}
