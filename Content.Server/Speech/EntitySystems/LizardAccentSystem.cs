using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
namespace Content.Server.Speech.EntitySystems;

public sealed class LizardAccentSystem : EntitySystem
{
    private static readonly Regex RegexLowerS = new("s+");
    private static readonly Regex RegexUpperS = new("S+");
    private static readonly Regex RegexInternalX = new(@"(\w)x");
    private static readonly Regex RegexLowerEndX = new(@"\bx([\-|r|R]|\b)");
    private static readonly Regex RegexUpperEndX = new(@"\bX([\-|r|R]|\b)");

    //Forge-change-start: Ru-Localization
    private static readonly Regex RegexLowerC = new Regex("с+");
    private static readonly Regex RegexUpperC = new Regex("С+");
    private static readonly Regex RegexLowerZ = new Regex("з+");
    private static readonly Regex RegexUpperZ = new Regex("З+");
    private static readonly Regex RegexLowerSh = new Regex("ш+");
    private static readonly Regex RegexUpperSh = new Regex("Ш+");
    private static readonly Regex RegexLowerCh = new Regex("ч+");
    private static readonly Regex RegexUpperCh = new Regex("Ч+");
    private static readonly Regex RegexLowerSch = new Regex("щ+");
    private static readonly Regex RegexUpperSch = new Regex("Щ+");
    //Forge-change-end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LizardAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, LizardAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // hissss
        message = RegexLowerS.Replace(message, "sss");
        // hiSSS
        message = RegexUpperS.Replace(message, "SSS");
        // ekssit
        message = RegexInternalX.Replace(message, "$1kss");
        // ecks
        message = RegexLowerEndX.Replace(message, "ecks$1");
        // eckS
        message = RegexUpperEndX.Replace(message, "ECKS$1");

        //Forge-change-start
        message = RegexLowerC.Replace(message, "сс");
        message = RegexUpperC.Replace(message, "СС");
        message = RegexLowerZ.Replace(message, "сс");
        message = RegexUpperZ.Replace(message, "СС");
        message = RegexLowerSh.Replace(message, "шш");
        message = RegexUpperSh.Replace(message, "ШШ");
        message = RegexLowerCh.Replace(message, "щщ");
        message = RegexUpperCh.Replace(message, "ЩЩ");
        message = RegexLowerSch.Replace(message, "щщ");
        message = RegexUpperSch.Replace(message, "ЩЩ");
        //Forge-change-end

        args.Message = message;
    }
}
