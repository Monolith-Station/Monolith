using System.Numerics;
using Content.Shared.GameTicking;
using Content.Shared.Parallax;
using Content.Shared._Crescent.SpaceBiomes;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared._Crescent.Vessel;
using Robust.Client.Player;
using Robust.Client.GameObjects;
using Content.Client.Parallax;
using Content.Shared.Roles;
using Robust.Shared.Map;

namespace Content.Client._Crescent.SpaceBiomes;

public sealed class SpaceBiomeSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly IPrototypeManager _protMan = default!;
    [Dependency] private readonly TransformSystem _formSys = default!;
    [Dependency] private readonly ParallaxSystem _parallaxSys = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private float _updTimer;
    private const float UpdateInterval = 0.5f; // in seconds - how often the checks for this system run

    private bool _dropTitle = false;
    private float _titleDropTimer = 0;
    private const float TitleDropTime = 10; // in seconds

    private EntityUid _playerUid; //used to keep playerUid for the initial title drop

    private SpaceBiomeSourceComponent? _cachedSource;
    private EntityUid? _cachedGrid;
    private EntityUid? _cachedMap;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerSpawn);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted) //otherwise this will tick like 5x faster on client. thanks prediction
            return;

        // //section purely for dropping da title of the station ur in, AFTER the biome drop
        // if (_dropTitle)
        // {
        //     _titleDropTimer += frameTime;
        //     if (_titleDropTimer > TitleDropTime)
        //     {
        //         _dropTitle = false;
        //         var gridData = GetGridInfo(_playerUid);
        //         if (gridData == null)
        //             return;
        //         NewVesselEnteredMessage message = new NewVesselEnteredMessage(gridData.Value.Item1, gridData.Value.Item2, gridData.Value.Item3);
        //         RaiseLocalEvent(_playerUid, ref message, true);
        //     }
        // }

        //update timer
        _updTimer += frameTime;
        if (_updTimer < UpdateInterval)
            return;
        _updTimer -= UpdateInterval;

        // 0. grab the local player ent
        if (_playerMan.LocalEntity == null) //this should never be null i thinky
            return;

        var localPlayerUid = _playerMan.LocalEntity.Value;
        var xform = Transform(localPlayerUid);
        var ourCoord = xform.Coordinates;

        // 1. grab the local grid, if any. if not, send msg signalling we entered space
        var newGrid = xform.GridUid;
        if (newGrid != _cachedGrid) //if true, we have changed grids since last update
        {
            _cachedGrid = newGrid;
            if (newGrid == null || TerminatingOrDeleted(newGrid))
            {
                var spaceMsg = new PlayerParentChangedMessage(null);
                RaiseLocalEvent(localPlayerUid, ref spaceMsg, true);
            }
            else
            {
                var message = new PlayerParentChangedMessage((EntityUid)newGrid);
                RaiseLocalEvent(localPlayerUid, ref message, true);
            }
        }
        // 2. grab the biome & check if its different than the cached biome from last update
        SpaceBiomeSourceComponent? newSource = null;
        var query = EntityQueryEnumerator<SpaceBiomeSourceComponent>();
        while (query.MoveNext(out var sourceUid, out var comp))
        {
            var otherCoord = Transform(sourceUid).Coordinates;
            if (!ourCoord.TryDistance(EntityManager, otherCoord, out var distance) || distance > (comp.SwapDistance ?? float.MaxValue)) //we're too far from this source, move on
                continue;

            if (newSource == null || //this whole shebang picks the highest priority source from the EQE
                    comp.Priority > newSource.Priority ||
                    comp.Priority == newSource.Priority && comp == newSource)
            {
                newSource = comp;
            }
        }
        // 3. check the mapid and check if its different than the cached mapid from the last update
        EntityUid? newMap = _formSys.GetMap(localPlayerUid);
        // 4. this is the actual checking bit
        // if the map changed then it cant be the same source from last update, so we do _cachedSource = newSource anyway.
        if (_cachedMap != newMap || _cachedSource != newSource)
        {
            _cachedMap = newMap;
            _cachedSource = newSource;
            var biome = _protMan.Index<SpaceBiomePrototype>(_cachedSource?.Id ?? "default");
            //note: this is where the parallax should swap. eventually.
            SpaceBiomeSwapMessage msg = new SpaceBiomeSwapMessage(biome);
            RaiseLocalEvent(localPlayerUid, ref msg, true);
        }
    }

    /// <summary>
    /// This turns on the dropTitle thing in the update function above.
    /// It's job is to make sure that, when you spawn in, the station you're on actually plays its music and shows its title as if you just entered
    /// otherwise only biome music will play until you re-enter the grid.
    /// </summary>
    /// <param name="args"></param>
    private void OnPlayerSpawn(LocalPlayerAttachedEvent args)
    {
        _playerUid = args.Entity;
        _titleDropTimer = 0;
        _dropTitle = true;
    }
}
