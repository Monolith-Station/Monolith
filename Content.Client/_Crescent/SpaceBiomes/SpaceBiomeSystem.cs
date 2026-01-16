using Content.Shared._Crescent.SpaceBiomes;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Client.Player;
using Robust.Client.GameObjects;
using Content.Shared.Shuttles.Components;
using Content.Client.Salvage;

namespace Content.Client._Crescent.SpaceBiomes;

public sealed class SpaceBiomeSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly IPrototypeManager _protMan = default!;
    [Dependency] private readonly TransformSystem _formSys = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    private float _updTimer;
    private const float UpdateInterval = 0.5f; // in seconds - how often the checks for this system run

    private SpaceBiomeSourceComponent? _cachedSource;
    private EntityUid? _cachedGrid;
    private EntityUid? _cachedMap;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FTLMapComponent, SpaceBiomeMapChangeMessage>(OnFTLMapChanged);
        SubscribeLocalEvent<SalvageExpeditionComponent, SpaceBiomeMapChangeMessage>(OnSalvageMapChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted) //otherwise this will tick like 5x faster on client. thanks prediction
            return;

        //update timers
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
            var message = new PlayerParentChangedMessage(newGrid); //if this is null it notifies that we're in space
            RaiseLocalEvent(localPlayerUid, ref message, true);

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
                    comp.Priority == newSource.Priority && comp == _cachedSource)
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
            var mapSwapMsg = new SpaceBiomeMapChangeMessage();
            if (newMap != null) //if the new map is null then :godo: we are borked anyway
            {
                RaiseLocalEvent((EntityUid)newMap, ref mapSwapMsg, true);
            }
            _cachedMap = newMap;
            _cachedSource = newSource;
            SpaceBiomePrototype biome;
            if (mapSwapMsg.Biome != null)
                biome = _protMan.Index<SpaceBiomePrototype>(mapSwapMsg.Biome);
            else
                biome = _protMan.Index<SpaceBiomePrototype>(newSource?.Id ?? "BiomeDefault");
            //note: this is where the parallax should swap. eventually.
            var biomeSwapMsg = new SpaceBiomeSwapMessage(biome);
            RaiseLocalEvent(localPlayerUid, ref biomeSwapMsg, true);

        }
    }

    private void OnFTLMapChanged(Entity<FTLMapComponent> ent, ref SpaceBiomeMapChangeMessage args)
    {
        args.Biome = ent.Comp.Biome;
    }

    private void OnSalvageMapChanged(Entity<SalvageExpeditionComponent> ent, ref SpaceBiomeMapChangeMessage args)
    {
        args.Biome = ent.Comp.Biome;
    }
}
