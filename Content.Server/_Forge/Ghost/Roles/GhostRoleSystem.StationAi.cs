using Content.Server.Ghost.Roles.Components;

namespace Content.Server.Ghost.Roles;

public sealed partial class GhostRoleSystem
{
    private readonly HashSet<EntityUid> _pendingReregistrations = new();
    private readonly List<EntityUid> _processingReregistrations = new();

    public void ReregisterGhostRole(Entity<GhostRoleComponent> role)
    {
        if (!role.Comp.ReregisterOnGhost)
            return;

        _pendingReregistrations.Add(role.Owner);
    }

    private void ProcessPendingReregistrations()
    {
        if (_pendingReregistrations.Count == 0)
            return;

        _processingReregistrations.Clear();
        _processingReregistrations.AddRange(_pendingReregistrations);
        _pendingReregistrations.Clear();

        foreach (var uid in _processingReregistrations)
        {
            if (!TryComp(uid, out GhostRoleComponent? role) || !role.ReregisterOnGhost)
                continue;

            if (_ghostRoles.TryGetValue(role.Identifier, out var registered) && registered.Owner == uid)
            {
                role.Taken = false;
                continue;
            }

            if (TryComp(uid, out GhostRoleRaffleComponent? raffle))
            {
                if (raffle.LifeStage <= ComponentLifeStage.Running)
                    RemoveRaffleAndUpdateEui(uid, raffle);

                _pendingReregistrations.Add(uid);
                continue;
            }

            role.Taken = false;
            RegisterGhostRole((uid, role));
        }
    }

    private TimeSpan GetRaffleEndTime(GhostRoleRaffleComponent? raffle)
    {
        if (raffle is null)
            return TimeSpan.MinValue;

        if (raffle.Countdown == TimeSpan.MaxValue)
            return _timing.CurTime;

        var currentTicks = _timing.CurTime.Ticks;
        var countdownTicks = raffle.Countdown.Ticks;
        if (countdownTicks > 0 && currentTicks > long.MaxValue - countdownTicks)
            return TimeSpan.MaxValue;
        if (countdownTicks < 0 && currentTicks < long.MinValue - countdownTicks)
            return TimeSpan.MinValue;

        return TimeSpan.FromTicks(currentTicks + countdownTicks);
    }

    private void ClearPendingReregistrations()
    {
        _pendingReregistrations.Clear();
        _processingReregistrations.Clear();
    }
}
