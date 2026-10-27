using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Medical.Components;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared._Mono.Cloning;
using Content.Shared.Database;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._Mono.Cloning;

public sealed partial class BodyScannerConsoleSystem : EntitySystem
{
    [Dependency] private DeviceLinkSystem _signalSystem = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private GeneticCloningSystem _geneticCloning = default!;
    [Dependency] private GeneticDatabaseSystem _database = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private GeneticCloningConsoleSystem _geneticConsole = default!;

    private const float UpdateRate = 1f;
    private float _updateDif;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyScannerConsoleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<BodyScannerConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BodyScannerConsoleComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<BodyScannerConsoleComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeLocalEvent<BodyScannerConsoleComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<BodyScannerConsoleComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<BodyScannerConsoleComponent, AfterActivatableUIOpenEvent>(OnUIOpen);
        SubscribeLocalEvent<BodyScannerConsoleComponent, BodyScannerScanMessage>(OnScan);
    }

    private void OnInit(EntityUid uid, BodyScannerConsoleComponent component, ComponentInit args)
    {
        _signalSystem.EnsureSourcePorts(uid, BodyScannerConsoleComponent.ScannerPort);
    }

    private void OnMapInit(EntityUid uid, BodyScannerConsoleComponent component, MapInitEvent args)
    {
        if (!TryComp<DeviceLinkSourceComponent>(uid, out var source))
            return;

        foreach (var port in source.Outputs.Values.SelectMany(ports => ports))
        {
            if (!TryComp<MedicalScannerComponent>(port, out var scanner))
                continue;

            component.Scanner = port;
            scanner.ConnectedConsole = uid;
        }
    }

    private void OnNewLink(EntityUid uid, BodyScannerConsoleComponent component, NewLinkEvent args)
    {
        if (TryComp<MedicalScannerComponent>(args.Sink, out var scanner) && args.SourcePort == BodyScannerConsoleComponent.ScannerPort)
        {
            component.Scanner = args.Sink;
            scanner.ConnectedConsole = uid;
        }

        UpdateUserInterface(uid, component);
    }

    private void OnPortDisconnected(EntityUid uid, BodyScannerConsoleComponent component, PortDisconnectedEvent args)
    {
        if (args.Port == BodyScannerConsoleComponent.ScannerPort)
            component.Scanner = null;

        UpdateUserInterface(uid, component);
    }

    private void OnAnchorChanged(EntityUid uid, BodyScannerConsoleComponent component, ref AnchorStateChangedEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnPowerChanged(EntityUid uid, BodyScannerConsoleComponent component, ref PowerChangedEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnUIOpen(EntityUid uid, BodyScannerConsoleComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnScan(EntityUid uid, BodyScannerConsoleComponent component, BodyScannerScanMessage args)
    {
        if (!_powerReceiverSystem.IsPowered(uid))
            return;

        if (!TryGetOccupant(uid, component, out var body, out var scanner))
            return;

        if (!_database.TryGetDatabase(uid, out var db))
            return;

        var record = _geneticCloning.CreateRecord(body, out var failReason);
        if (record == null)
        {
            if (failReason != null)
                _popup.PopupEntity(failReason, uid, args.Actor);

            UpdateUserInterface(uid, component);
            return;
        }

        _database.Upsert(db.Value, record);
        _geneticConsole.UpdateOpenInterfaces();
        _popup.PopupEntity(Loc.GetString("body-scanner-console-scan-complete", ("name", record.Name)), uid, args.Actor);
        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):actor} scanned {ToPrettyString(body):subject} into genetic database {ToPrettyString(db.Value.Owner)} using {ToPrettyString(uid)}.");

        UpdateUserInterface(uid, component);
    }

    private bool TryGetOccupant(EntityUid uid, BodyScannerConsoleComponent component, out EntityUid body, out MedicalScannerComponent scanner)
    {
        body = default;
        scanner = default!;

        if (component.Scanner is not { } scannerUid ||
            !TryComp(scannerUid, out MedicalScannerComponent? scannerComp) ||
            !Transform(scannerUid).Anchored ||
            !IsScannerInRange(uid, scannerUid, component))
        {
            return false;
        }

        scanner = scannerComp;
        if (scanner.BodyContainer.ContainedEntity is not { } contained)
            return false;

        body = contained;
        return true;
    }

    private bool IsScannerInRange(EntityUid uid, EntityUid scanner, BodyScannerConsoleComponent component)
    {
        return Transform(scanner).Coordinates.TryDistance(EntityManager, Transform(uid).Coordinates, out var distance) &&
               distance <= component.MaxDistance;
    }

    public void UpdateUserInterface(EntityUid uid, BodyScannerConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!_uiSystem.HasUi(uid, BodyScannerConsoleUiKey.Key))
            return;

        if (!_powerReceiverSystem.IsPowered(uid))
        {
            _uiSystem.CloseUis(uid);
            return;
        }

        _uiSystem.SetUiState(uid, BodyScannerConsoleUiKey.Key, GetUserInterfaceState(uid, component));
    }

    private BodyScannerConsoleBoundUserInterfaceState GetUserInterfaceState(EntityUid uid, BodyScannerConsoleComponent component)
    {
        string? databaseName = null;
        Entity<GeneticDatabaseComponent>? db = null;
        if (_database.TryGetDatabase(uid, out var found))
        {
            db = found;
            databaseName = _database.GetDisplayName(found.Value);
        }

        if (component.Scanner == null || !HasComp<MedicalScannerComponent>(component.Scanner) || !Transform(component.Scanner.Value).Anchored)
            return new BodyScannerConsoleBoundUserInterfaceState(null, BodyScanStatus.NoScanner, null, false, databaseName);

        if (!IsScannerInRange(uid, component.Scanner.Value, component))
            return new BodyScannerConsoleBoundUserInterfaceState(null, BodyScanStatus.ScannerFar, null, false, databaseName);

        if (!TryGetOccupant(uid, component, out var body, out _) || !HasComp<MobStateComponent>(body))
            return new BodyScannerConsoleBoundUserInterfaceState(null, BodyScanStatus.Empty, null, false, databaseName);

        var name = MetaData(body).EntityName;

        // validate here
        if (!_geneticCloning.CanRecord(body, out var mindId, out var failReason))
            return new BodyScannerConsoleBoundUserInterfaceState(name, BodyScanStatus.Invalid, failReason, false, databaseName);

        var onFile = db != null && _database.HasRecord(db.Value, mindId);
        return new BodyScannerConsoleBoundUserInterfaceState(name, BodyScanStatus.Ready, null, onFile, databaseName);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateDif += frameTime;
        if (_updateDif < UpdateRate)
            return;

        _updateDif -= UpdateRate;

        var query = EntityQueryEnumerator<BodyScannerConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_uiSystem.IsUiOpen(uid, BodyScannerConsoleUiKey.Key))
                UpdateUserInterface(uid, component);
        }
    }
}
