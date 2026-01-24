using Content.Shared._DV.Traits.Effects;
using Content.Shared._EinsteinEngines.Language;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Traits.Effects;

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

    public override void Apply(TraitEffectContext ctx)
    {
        // This effect needs to be applied server-side where we have access to LanguageSystem.
        // The actual spawning logic is handled by the server TraitSystem.
        // This class just holds the data.
        throw new NotImplementedException("LanguageTraitEffect should not have its Apply method called");
    }
}

/// <summary>
/// Effect that gives languages to a player.
/// </summary>
public sealed partial class AddLanguagesEffect : BaseLanguageTraitEffect;

/// <summary>
/// Effect that removes languages from a player.
/// </summary>
public sealed partial class RemoveLanguagesEffect : BaseLanguageTraitEffect;
