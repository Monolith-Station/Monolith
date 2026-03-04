using Content.Server._Mono.Speech.Components;
using Content.Server.Speech;
using Content.Server.Speech.EntitySystems;
using System.Text.RegularExpressions;

namespace Content.Server._Mono.Speech.EntitySystems;

public sealed class HydrakinAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HydrakinAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    // converts left word when typed into the right word. For example typing you becomes ye.
    public string Accentuate(string message, HydrakinAccentComponent component)
    {
        var msg = message;

        msg = _replacement.ApplyReplacements(msg, "hydrakin");

        return msg;
    }

    private void OnAccentGet(EntityUid uid, HydrakinAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message, component);
    }
}
