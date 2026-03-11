using Content.Server._EinsteinEngines.Language;
using Content.Shared._DV.Traits.Effects;
using Content.Shared._EinsteinEngines.Language;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Traits.Effects;

/// <summary>
/// Base class for all effects that handle a list of languages
/// </summary>
public abstract partial class BaseLanguageTraitEffect : BaseTraitEffect
{
    /// <summary>
    /// The entity prototype to spawn.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<LanguagePrototype>> Languages = default!;

    /// <summary>
    /// Whether to affect understanding of the language.
    /// </summary>
    [DataField(required: true)]
    public bool Understood = default!;

    /// <summary>
    /// Whether to affect speech of the language.
    /// </summary>
    [DataField(required: true)]
    public bool Spoken = default!;
}

/// <summary>
/// Effect that gives languages to a player.
/// </summary>
public sealed partial class AddLanguagesEffect : BaseLanguageTraitEffect
{
    public override void Apply(TraitEffectContext ctx)
    {
        if (!ctx.EntMan.TrySystem<LanguageSystem>(out var languageSys))
            return;

        foreach (var language in Languages)
            languageSys.AddLanguage(ctx.Player, language, Spoken, Understood);
    }
}

/// <summary>
/// Effect that removes languages from a player.
/// </summary>
public sealed partial class RemoveLanguagesEffect : BaseLanguageTraitEffect
{
    public override void Apply(TraitEffectContext ctx)
    {
        if (!ctx.EntMan.TrySystem<LanguageSystem>(out var languageSys))
            return;

        foreach (var language in Languages)
            languageSys.RemoveLanguage(ctx.Player, language, Spoken, Understood);
    }
}
