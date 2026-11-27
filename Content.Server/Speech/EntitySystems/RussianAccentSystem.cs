using Content.Server.Speech.Components;

namespace Content.Server.Speech.EntitySystems;

// Monolith: Now uses a replacementsfile
public sealed partial class RussianAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<RussianAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, RussianAccentComponent component, AccentGetEvent args)
    {
        args.Message = _replacement.ApplyReplacements(args.Message, "russian"); // Monolith
    }
}
