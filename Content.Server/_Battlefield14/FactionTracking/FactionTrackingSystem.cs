using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Battlefield14.FactionTracking;

public sealed class FactionTrackingSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;

    private static readonly HashSet<string> BluforDepartments = new() { "USMC", "USSF", "ARNG", "USPD", "BW" };
    private static readonly HashSet<string> RedforDepartments = new() { "VDV", "VKS", "OMON", "FSB" };

    private float _updateTimer;

    public override void Update(float frameTime)
    {
        _updateTimer += frameTime;
        if (_updateTimer < 1.0f)
            return;

        _updateTimer = 0f;

        var ticker = EntityManager.System<GameTicker>();
        var runLevel = ticker.RunLevel;
        var isRoundStarted = runLevel != GameRunLevel.PreRoundLobby;

        int bluforReady = 0;
        int redforReady = 0;
        int totalReady = 0;
        int bluforAlive = 0;
        int redforAlive = 0;

        if (isRoundStarted)
        {
            (bluforAlive, redforAlive) = CountAliveByFaction();
        }
        else
        {
            (bluforReady, redforReady, totalReady) = CountReadyByFaction(ticker);
        }

        var ev = new TickerFactionCountEvent(isRoundStarted, bluforReady, redforReady, totalReady, bluforAlive, redforAlive);
        RaiseNetworkEvent(ev);
    }

    private (int blufor, int redfor, int total) CountReadyByFaction(GameTicker ticker)
    {
        var blufor = 0;
        var redfor = 0;
        var total = 0;

        foreach (var (userId, status) in ticker.PlayerGameStatuses)
        {
            if (status != PlayerGameStatus.ReadyToPlay)
                continue;

            total++;

            if (!_playerManager.TryGetSessionById(userId, out var session))
                continue;

            var departmentId = GetPreferredDepartment(session);
            if (departmentId == null)
                continue;

            if (BluforDepartments.Contains(departmentId))
                blufor++;
            else if (RedforDepartments.Contains(departmentId))
                redfor++;
        }

        return (blufor, redfor, total);
    }

    private (int blufor, int redfor) CountAliveByFaction()
    {
        var blufor = 0;
        var redfor = 0;

        var query = EntityQueryEnumerator<MindComponent>();

        while (query.MoveNext(out var mindId, out var mind))
        {
            if (mind.OwnedEntity == null)
                continue;

            if (!_jobs.MindTryGetJobId(mindId, out var jobProtoId))
                continue;

            if (!_jobs.TryGetDepartment(jobProtoId!.Value, out var department))
                continue;

            if (BluforDepartments.Contains(department.ID))
            {
                if (_mobState.IsAlive(mind.OwnedEntity.Value))
                    blufor++;
            }
            else if (RedforDepartments.Contains(department.ID))
            {
                if (_mobState.IsAlive(mind.OwnedEntity.Value))
                    redfor++;
            }
        }

        return (blufor, redfor);
    }

    private string? GetPreferredDepartment(ICommonSession session)
    {
        var profile = (HumanoidCharacterProfile)_prefsManager.GetPreferences(session.UserId).SelectedCharacter;
        var jobPriorities = profile.JobPriorities;

        ProtoId<JobPrototype>? bestJob = null;
        JobPriority bestPriority = JobPriority.Never;

        foreach (var (jobId, priority) in jobPriorities)
        {
            if (priority == JobPriority.Never)
                continue;

            if (priority > bestPriority)
            {
                bestPriority = priority;
                bestJob = jobId;
            }
        }

        if (bestJob == null)
            return null;

        if (_jobs.TryGetDepartment(bestJob.Value, out var department))
            return department.ID;

        return null;
    }
}
