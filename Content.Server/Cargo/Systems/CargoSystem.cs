using Content.Server.Cargo.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Popups;
using Content.Server.Shuttles.Systems;
using Content.Server.Stack;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Content.Server._NF.SectorServices; // Frontier
using Content.Shared.Whitelist;
using Content.Server._NF.Bank; // Frontier

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem : SharedCargoSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private DeviceLinkSystem _linker = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private PaperSystem _paperSystem = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ShuttleConsoleSystem _console = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private MetaDataSystem _metaSystem = default!;
    [Dependency] private SectorServiceSystem _sectorService = default!; // Frontier
    [Dependency] private EntityWhitelistSystem _whitelist = default!; // Frontier
    [Dependency] private BankSystem _bank = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<CargoSellBlacklistComponent> _blacklistQuery;
    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<TradeStationComponent> _tradeQuery;

    private HashSet<EntityUid> _setEnts = new();
    private List<EntityUid> _listEnts = new();
    private List<(EntityUid, CargoPalletComponent, TransformComponent)> _pads = new();

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();
        _blacklistQuery = GetEntityQuery<CargoSellBlacklistComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();
        _tradeQuery = GetEntityQuery<TradeStationComponent>();

        InitializeConsole();
        InitializeShuttle();
        InitializeTelepad();
        InitializeBounty();
        // Frontier: add specific initialization calls here.
        InitializePirateBounty();
        InitializeTradeCrates();
        // End Frontier
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateConsole(frameTime);
        UpdateTelepad(frameTime);
        UpdateBounty();
    }

    [PublicAPI]
    public void UpdateBankAccount(EntityUid uid, StationBankAccountComponent component, int balanceAdded)
    {
        component.Balance += balanceAdded;
        var query = EntityQueryEnumerator<BankClientComponent, TransformComponent>();

        var ev = new BankBalanceUpdatedEvent(uid, component.Balance);
        while (query.MoveNext(out var client, out var comp, out var xform))
        {
            var station = _station.GetOwningStation(client, xform);
            if (station != uid)
                continue;

            comp.Balance = component.Balance;
            Dirty(client, comp);
            RaiseLocalEvent(client, ref ev);
        }
    }
}
