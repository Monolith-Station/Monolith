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

    public override void Apply(TraitEffectContext ctx)
    {
        // This effect needs to be applied server-side where we have access to LanguageSystem.
        // The actual spawning logic is handled by the server TraitSystem.
        // This class just holds the data.
        throw new NotImplementedException("LanguageTraitEffect should not have its Apply method called");
    }
}

/// <summary>
/// Effect that gives spoken languages to a a player.
/// </summary>
public sealed partial class AddLanguagesSpokenEffect : BaseLanguageTraitEffect;

/// <summary>
/// Effect that gives understood languages to a a player.
/// </summary>
public sealed partial class AddLanguagesUnderstoodEffect : BaseLanguageTraitEffect;

/// <summary>
/// Effect that removes spoken languages from a player.
/// </summary>
public sealed partial class RemoveLanguagesSpokenEffect : BaseLanguageTraitEffect;

/// <summary>
/// Effect that removes understood languages from a player.
/// </summary>
public sealed partial class RemoveLanguagesUnderstoodEffect : BaseLanguageTraitEffect;
