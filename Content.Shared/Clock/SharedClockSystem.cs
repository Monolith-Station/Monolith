using System.Linq;
using Content.Shared.Examine;
using Content.Shared.GameTicking;

namespace Content.Shared.Clock;

public abstract class SharedClockSystem : EntitySystem
{
    [Dependency] private readonly SharedGameTicker _ticker = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ClockComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<ClockComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("clock-examine", ("time", GetClockTimeText(ent))));
    }

    public string GetClockTimeText(Entity<ClockComponent> ent)
    {
        var time = GetClockTime(ent);
        return time.ToString("hh\\:mm"); // Frontier: always 24-hour time (so 0:00 is 0:00, not 12:00)
    }

    private TimeSpan GetGlobalTime()
    {
        return (EntityQuery<GlobalTimeManagerComponent>().FirstOrDefault()?.TimeOffset ?? TimeSpan.Zero) + _ticker.RoundDuration();
    }

    public TimeSpan GetClockTime(Entity<ClockComponent> ent)
    {
        var comp = ent.Comp;

        if (comp.StuckTime != null)
            return comp.StuckTime.Value;

        return GetGlobalTime(); // Frontier: all clocks are 24 hour clocks
    }
}
