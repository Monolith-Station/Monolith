using System.Linq;
using System.Numerics;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared._Mono.Mining;
using Content.Shared._NF.Shuttles.Events;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Mining;

public sealed partial class OreScannerSystem : EntitySystem
{
    [Dependency] private BuriedOreSystem _deposits = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedRadarConsoleSystem _radar = default!;
    [Dependency] private ISharedAdminManager _admins = default!;
    private readonly List<BuriedOreVein> _veins = new();
    private float _timer;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OreScannerComponent, BeforeActivatableUIOpenEvent>(OnBeforeOpen);
    }

    private void OnBeforeOpen(Entity<OreScannerComponent> ent, ref BeforeActivatableUIOpenEvent args)
        => UpdateState(ent.Owner);

    public override void Update(float frameTime)
    {
        _timer += frameTime;
        if (_timer < 1f)
            return;
        _timer %= 1f;
        var query = EntityQueryEnumerator<OreScannerComponent>();
        while (query.MoveNext(out var uid, out var scanner))
        {
            if (!_ui.IsUiOpen(uid, OreScannerUiKey.Key))
                continue;
            if (!_deposits.TryGetSurface(uid, out _))
            {
                _ui.CloseUi(uid, OreScannerUiKey.Key);
                continue;
            }
            UpdateState(uid);
        }
    }

    private void UpdateState(EntityUid uid)
    {
        if (!_deposits.TryGetSurface(uid, out var surface))
            return;
        var scanner = Comp<OreScannerComponent>(uid);
        var range = Math.Clamp(scanner.Range, 1f, 1024f);
        if (TryComp<RadarConsoleComponent>(uid, out var radar) && radar.MaxRange != range)
            _radar.SetRange(uid, range, radar);
        _deposits.Scan(surface, _deposits.GetPosition(uid, surface), range * 1.5f, _veins);
        var scans = new OreDepositScan[_veins.Count];
        for (var i = 0; i < scans.Length; i++)
        {
            var vein = _veins[i];
            var mineral = _prototypes.Index(vein.Mineral);
            var estimate = vein.Remaining == 0 ? 0 : Math.Max(100, (int)Math.Round(vein.Remaining / 100f) * 100);
            scans[i] = new OreDepositScan(Loc.GetString(mineral.Name), mineral.Color, vein.Boundary,
                estimate, vein.Saturation, vein.Rate);
        }
        var navigation = new NavInterfaceState(range,
            GetNetCoordinates(new EntityCoordinates(uid, Vector2.Zero)), Angle.Zero,
            new Dictionary<NetEntity, List<DockingPortState>>(), InertiaDampeningMode.Dampen);
        _ui.SetUiState(uid, OreScannerUiKey.Key, new OreScannerState(navigation, GetNetEntity(surface), scans));
    }
}
