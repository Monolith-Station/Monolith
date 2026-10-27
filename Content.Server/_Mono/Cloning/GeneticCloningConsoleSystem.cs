using Content.Server.Administration.Logs;
using Content.Server.Cloning.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Mono.Cloning;
using Content.Shared.Cloning;
using Content.Shared.Cloning.CloningConsole;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._Mono.Cloning;

/// <summary>
/// record UI of cloning console, listes records from the grid
/// </summary>
public sealed partial class GeneticCloningConsoleSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private GeneticCloningSystem _geneticCloning = default!;
    [Dependency] private GeneticDatabaseSystem _database = default!;
    [Dependency] private PowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;

    private const float UpdateRate = 5f;
    private float _updateDif;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneticCloningConsoleComponent, GeneticCloningConsoleCloneRecordMessage>(OnCloneRecord);
        SubscribeLocalEvent<GeneticCloningConsoleComponent, GeneticCloningConsoleDeleteRecordMessage>(OnDeleteRecord);
    }

    private void OnCloneRecord(EntityUid uid, GeneticCloningConsoleComponent component, GeneticCloningConsoleCloneRecordMessage args)
    {
        if (!_powerReceiverSystem.IsPowered(uid))
            return;

        TryClone(uid, args.Id, args.Actor);
        UpdateUserInterface(uid);
    }

    private void OnDeleteRecord(EntityUid uid, GeneticCloningConsoleComponent component, GeneticCloningConsoleDeleteRecordMessage args)
    {
        if (!_powerReceiverSystem.IsPowered(uid))
            return;

        if (_database.TryGetDatabase(uid, out var db) &&
            _database.TryGetRecord(db.Value, args.Id, out var record) &&
            _database.Remove(db.Value, record))
        {
            _adminLogger.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(args.Actor):actor} deleted the genetic record of {record.Name} from {ToPrettyString(db.Value.Owner)} using {ToPrettyString(uid)}.");
        }

        UpdateOpenInterfaces();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateDif += frameTime;
        if (_updateDif < UpdateRate)
            return;

        _updateDif -= UpdateRate;

        UpdateOpenInterfaces();
    }

    /// <summary>
    /// Refresh console ui
    /// </summary>
    public void UpdateOpenInterfaces()
    {
        var query = EntityQueryEnumerator<GeneticCloningConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_uiSystem.IsUiOpen(uid, GeneticCloningConsoleUiKey.Key))
                UpdateUserInterface(uid);
        }
    }

    public void TryClone(EntityUid uid, int recordId, EntityUid actor, CloningConsoleComponent? consoleComponent = null)
    {
        if (!Resolve(uid, ref consoleComponent))
            return;

        if (consoleComponent.CloningPod is not { } podUid || !TryComp<CloningPodComponent>(podUid, out var pod))
            return;

        if (!Transform(podUid).Anchored || !consoleComponent.CloningPodInRange)
            return;

        if (!_database.TryGetDatabase(uid, out var db) || !_database.TryGetRecord(db.Value, recordId, out var record))
            return;

        if (_database.GetStatus(record) != GeneticRecordStatus.Cloneable)
            return;

        if (_geneticCloning.TryCloning(podUid, record, pod))
            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(actor):actor} cloned {record.Name} ({ToPrettyString(record.Mind)}) from {ToPrettyString(db.Value.Owner)} using {ToPrettyString(uid)}.");
    }

    public void UpdateUserInterface(EntityUid uid, CloningConsoleComponent? consoleComponent = null)
    {
        if (!Resolve(uid, ref consoleComponent, false))
            return;

        if (!_uiSystem.HasUi(uid, GeneticCloningConsoleUiKey.Key))
            return;

        if (!_powerReceiverSystem.IsPowered(uid))
        {
            _uiSystem.CloseUis(uid);
            return;
        }

        _uiSystem.SetUiState(uid, GeneticCloningConsoleUiKey.Key, GetUserInterfaceState(uid, consoleComponent));
    }

    private GeneticCloningConsoleBoundUserInterfaceState GetUserInterfaceState(EntityUid uid, CloningConsoleComponent consoleComponent)
    {
        var clonerStatus = ClonerStatus.Ready;

        // I probably shouldnt use the name database and db
        string? databaseName = null;
        var records = new List<GeneticRecordEntry>();
        if (_database.TryGetDatabase(uid, out var db))
        {
            databaseName = db.Value.Comp.DatabaseName;
            records = _database.GetEntries(db.Value);
        }

        var cloneBodyInfo = Loc.GetString("generic-unknown");
        var clonerConnected = false;
        var clonerMindPresent = false;
        var clonerInRange = consoleComponent.CloningPodInRange;
        if (consoleComponent.CloningPod != null && TryComp<CloningPodComponent>(consoleComponent.CloningPod, out var clonePod)
            && Transform(consoleComponent.CloningPod.Value).Anchored)
        {
            clonerConnected = true;
            var cloneBody = clonePod.BodyContainer.ContainedEntity;

            clonerMindPresent = clonePod.Status == CloningPodStatus.Cloning;
            if (HasComp<ActiveCloningPodComponent>(consoleComponent.CloningPod) && clonePod.Status != CloningPodStatus.Idle)
            {
                if (cloneBody != null)
                    cloneBodyInfo = Identity.Name(cloneBody.Value, EntityManager);
                clonerStatus = ClonerStatus.ClonerOccupied;
            }
        }
        else
        {
            clonerStatus = ClonerStatus.NoClonerDetected;
        }

        return new GeneticCloningConsoleBoundUserInterfaceState(
            cloneBodyInfo,
            clonerMindPresent,
            clonerStatus,
            clonerConnected,
            clonerInRange,
            databaseName,
            records);
    }
}
