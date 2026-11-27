using Content.Shared.Examine;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Mono.Mining;

public sealed partial class SharedOreDrillSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OreDrillComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<OreDrillComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Deposit is not { } deposit)
        {
            args.PushMarkup(Loc.GetString("ore-drill-patch-none"));
            return;
        }
        var mineral = _prototypes.Index(deposit);
        var name = Loc.GetString(mineral.Name);
        args.PushMarkup(Loc.GetString("ore-drill-patch",
            ("mineralName", name),
            ("mineral", $"[color={mineral.Color.WithAlpha(1f).ToHex()}]{FormattedMessage.EscapeText(name)}[/color]")));
    }
}
