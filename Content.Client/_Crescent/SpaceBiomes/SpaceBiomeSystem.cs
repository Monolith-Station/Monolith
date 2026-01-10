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

namespace Content.Client._Crescent.SpaceBiomes;

public sealed class SpaceBiomeSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly IPrototypeManager _protMan = default!;
    [Dependency] private readonly TransformSystem _formSys = default!;
    [Dependency] private readonly ParallaxSystem _parallaxSys = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private float _updTimer;
    private const float UpdateInterval = 5; //in seconds //

    private bool _dropTitle = false;
    private float _titleDropTimer = 0;
    private const float TitleDropTime = 20; // in seconds

    private EntityUid _playerUid; //used to keep playerUid for the initial title drop

    public SpaceBiomeSourceComponent? currentSource;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerSpawn);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted) //otherwise this will tick like 5x faster on client. thanks prediction
            return;

        Log.Info("update timer: " + _updTimer.ToString());
        Log.Info("title drop timer: " + _titleDropTimer.ToString());
        if (_dropTitle)
        {
            _titleDropTimer += frameTime;
            if (_titleDropTimer > TitleDropTime)
            {
                _dropTitle = false;
                var gridData = GetGridInfo(_playerUid);
                if (gridData == null)
                    return;
                NewVesselEnteredMessage message = new NewVesselEnteredMessage(gridData.Value.Item1, gridData.Value.Item2, gridData.Value.Item3);
                RaiseLocalEvent(_playerUid, ref message, true);
            }
        }


        _updTimer += frameTime;
        if (_updTimer < UpdateInterval)
            return;
        _updTimer = 0;

        if (_playerMan.LocalEntity == null) //this should never be null i thinky
            return;

        var localPlayerUid = _playerMan.LocalEntity.Value;

        var playerPos = _formSys.GetWorldPosition(Transform(localPlayerUid));

        SpaceBiomeSourceComponent? newSource = null;

        var query = EntityQueryEnumerator<SpaceBiomeSourceComponent>();

        while (query.MoveNext(out var sourceUid, out var comp))
        {
            if ((_formSys.GetWorldPosition(sourceUid) - playerPos).Length() > comp.SwapDistance)
                continue;

            if (newSource == null ||
                    comp.Priority > newSource.Priority ||
                    comp.Priority == newSource.Priority && comp == currentSource)
            {
                newSource = comp;
            }
        }
        if (newSource == currentSource)
            return;

        currentSource = newSource;
        SwapBiome(localPlayerUid, newSource);
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

    private void OnParentChanged(ref EntParentChangedMessage args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var gridData = GetGridInfo(args.Entity);
        if (gridData == null)
            return;

        NewVesselEnteredMessage message = new NewVesselEnteredMessage(gridData.Value.Item1, gridData.Value.Item2, gridData.Value.Item3);
        RaiseLocalEvent(args.Entity, ref message, true);
    }

    private void SwapBiome(EntityUid uid, SpaceBiomeSourceComponent? source)
    {
        EntityUid? mapUid = _formSys.GetMap(uid);
        if (mapUid == null)
            return;

        SpaceBiomePrototype biome = _protMan.Index<SpaceBiomePrototype>(source?.Id ?? "default");
        _parallaxSys.SwapParallax(uid, EnsureComp<ParallaxComponent>(uid), biome.Parallax, biome.SwapDuration);

        SpaceBiomeSwapMessage msg = new SpaceBiomeSwapMessage(biome);
        RaiseLocalEvent(uid, ref msg, true);
    }

    private (string, string, string)? GetGridInfo(EntityUid entity)
    {
        if (TerminatingOrDeleted(entity))
            return null;

        var parentStation = Transform(entity).GridUid;

        if (parentStation == null)
            return null;

        var name = MetaData((EntityUid)parentStation).EntityName;

        var description = ""; //fallback for description is nothin'
        if (TryComp<VesselInfoComponent>(parentStation, out var vesselinfo))
            description = vesselinfo.Description;

        var musicPrototype = "";

        if (TryComp<VesselMusicComponent>(parentStation, out var music)) //if this succeeds, we have custom music! if it fails,
            musicPrototype = music.AmbientMusicPrototype;                                   //the component is missing and we just keep ""

        return (name, description, musicPrototype);
    }
}
