using Robust.Shared.Timing;

namespace Content.Shared.Verbs;

/// <summary>
/// A tiny confirmation helper function for impactful context menu actions you don't want to accidentally perform
/// provides logic you can feed into for forcing players to perform a verb twice to initiate an action
/// use a different verbKey to diffrentiate between actions (ie "suicide_melee" or "suicide_gun")
/// this only provides the delay - be sure to add feedback (sounds, popups, etc)
/// </summary>
public static class VerbConfirmationSystem
{
    // (user:verbKey) : TimeSpan
    private static readonly Dictionary<(EntityUid, string), TimeSpan> ConfirmTimer = new();

    public static bool Check(EntityUid user, string verbKey, IGameTiming timing, TimeSpan? delay = null)
    {
        delay ??= TimeSpan.FromSeconds(5); // Default 5 seconds, but possible to alter via arg
        var now = timing.CurTime;
        var id = (user, verbKey);

        if (!ConfirmTimer.TryGetValue(id, out var nextConfirm) || now > nextConfirm)
        {
            ConfirmTimer[id] = now + delay.Value;
            return false;
        }

        ConfirmTimer.Remove(id);
        return true;
    }
}
