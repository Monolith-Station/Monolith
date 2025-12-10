using Content.Shared.Roles;
using Content.Server._EinsteinEngines.Language;

namespace Content.Server.Jobs;

public sealed partial class AddLanguageSpecial : JobSpecial
{
    /// <summary>
    ///     The list of all Spoken Languages that this trait adds.
    /// </summary>
    [DataField]
    public List<string>? LanguagesSpoken { get; private set; } = default!;

    /// <summary>
    ///     The list of all Understood Languages that this trait adds.
    /// </summary>
    [DataField]
    public List<string>? LanguagesUnderstood { get; private set; } = default!;

    /// <summary>
    ///     The list of all Spoken Languages that this trait removes.
    /// </summary>
    [DataField]
    public List<string>? RemoveLanguagesSpoken { get; private set; } = default!;

    /// <summary>
    ///     The list of all Understood Languages that this trait removes.
    /// </summary>
    [DataField]
    public List<string>? RemoveLanguagesUnderstood { get; private set; } = default!;

    public override void AfterEquip(EntityUid mob)
    {
        var language = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<LanguageSystem>();


        if (RemoveLanguagesSpoken is not null)
            foreach (var lang in RemoveLanguagesSpoken)
                language.RemoveLanguage(mob, lang, true, false);

        if (RemoveLanguagesUnderstood is not null)
            foreach (var lang in RemoveLanguagesUnderstood)
                language.RemoveLanguage(mob, lang, false, true);

        if (LanguagesSpoken is not null)
            foreach (var lang in LanguagesSpoken)
                language.AddLanguage(mob, lang, true, false);

        if (LanguagesUnderstood is not null)
            foreach (var lang in LanguagesUnderstood)
                language.AddLanguage(mob, lang, false, true);
    }
}
