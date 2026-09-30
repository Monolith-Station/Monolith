using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class ResomiAccentSystem : EntitySystem
{

    [Dependency] private IRobustRandom _random = default!;

    private static readonly Regex RegexLowerSilly = new Regex("silly");
    private static readonly Regex RegexFirstCapSilly = new Regex("Silly");
    private static readonly Regex RegexUpperSilly = new Regex("SILLY");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResomiAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, ResomiAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        if (_random.Prob(component.BawkChance))
        {

            // bawk
            message = RegexLowerSilly.Replace(message, "silly... bawk");
            // Bawk
            message = RegexFirstCapSilly.Replace(message, "Silly... Bawk");
            // BAWK
            message = RegexUpperSilly.Replace(message, "SILLY... BAWK");
        }

        args.Message = message;
    }
}
