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

namespace Content.Client._Crescent.SpaceBiomes;

public sealed class SpaceBiomeSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly IPrototypeManager _protMan = default!;
    [Dependency] private readonly TransformSystem _formSys = default!;
    [Dependency] private readonly ParallaxSystem _parallaxSys = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private float _updTimer;
    private const float UpdateInterval = 5; //in seconds

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceBiomeTrackerComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerSpawn);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updTimer += frameTime;
        if (_updTimer < UpdateInterval)
            return;
        _updTimer = 0;

        if (_playerMan.LocalEntity == null) //this should never be null i thinky
            return;

        var localPlayerUid = _playerMan.LocalEntity.Value;

        var playerPos = _formSys.GetWorldPosition(Transform(localPlayerUid));
        var tracker = EnsureComp<SpaceBiomeTrackerComponent>(localPlayerUid);

        SpaceBiomeSourceComponent? newSource = null;

        var query = EntityQueryEnumerator<SpaceBiomeSourceComponent>();

        while (query.MoveNext(out var sourceUid, out var comp))
        {
            if ((_formSys.GetWorldPosition(sourceUid) - playerPos).Length() > comp.SwapDistance)
                continue;

            if (newSource == null ||
                    comp.Priority > newSource.Priority ||
                    comp.Priority == newSource.Priority && comp == tracker.Source)
            {
                newSource = comp;
            }
        }
        if (newSource == tracker.Source)
            return;

        tracker.Source = newSource;
        tracker.Biome = newSource?.Biome ?? "default";
        SwapBiome(localPlayerUid, newSource);
    }

    /// <summary>
    /// HULLROT: This specifically makes the station's designation show up 10 seconds after you spawn in. This is exclusively for music, and to show cool title at the top of ur screen.
    /// </summary>
    /// <param name="args"></param>
    private void OnPlayerSpawn(LocalPlayerAttachedEvent args)
    {
        EntityUid uid = args.Entity;

        var gridData = GetGridInfo(uid);
        if (gridData == null)
            return;

        Timer.Spawn(TimeSpan.FromSeconds(10), () =>
        {
            NewVesselEnteredMessage message = new NewVesselEnteredMessage(gridData.Value.Item1, gridData.Value.Item2, gridData.Value.Item3);
            RaiseLocalEvent(uid, ref message, true);
        });
    }

    private void OnParentChanged(EntityUid uid, SpaceBiomeTrackerComponent component, EntParentChangedMessage args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var gridData = GetGridInfo(uid);
        if (gridData == null)
            return;

        NewVesselEnteredMessage message = new NewVesselEnteredMessage(gridData.Value.Item1, gridData.Value.Item2, gridData.Value.Item3);
        RaiseLocalEvent(uid, ref message, true);
    }

    private void SwapBiome(EntityUid uid, SpaceBiomeSourceComponent? source)
    {
        EntityUid? mapUid = _formSys.GetMap(uid);
        if (mapUid == null)
            return;

        SpaceBiomePrototype biome = _protMan.Index<SpaceBiomePrototype>(source?.Biome ?? "default");
        _parallaxSys.SwapParallax(uid, EnsureComp<ParallaxComponent>(uid), biome.Parallax, biome.SwapDuration);

        SpaceBiomeSwapMessage msg = new SpaceBiomeSwapMessage(source?.Biome ?? "default");
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
