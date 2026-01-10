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

    private Dictionary<Vector2, HashSet<EntityUid>> _chunks = new();
    private float _updTimer;
    private const int ChunkSize = 1000; //in meters
    private const float UpdateInterval = 5; //in seconds

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceBiomeSourceComponent, ComponentInit>(OnSourceInit);
        SubscribeLocalEvent<SpaceBiomeSourceComponent, ComponentShutdown>(OnSourceShutdown);
        SubscribeLocalEvent<SpaceBiomeTrackerComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
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

    private void OnRestart(RoundRestartCleanupEvent ev)
    {
        _chunks.Clear();
    }

    private void OnSourceInit(Entity<SpaceBiomeSourceComponent> uid, ref ComponentInit args)
    {
        AddBiome(uid, uid.Comp);
    }

    private void OnSourceShutdown(Entity<SpaceBiomeSourceComponent> uid, ref ComponentShutdown args)
    {
        RemoveBiome(uid, uid.Comp);
    }

    /// <summary>
    /// HULLROT: This specifically makes the station's designation show up 10 seconds after you spawn in. This is exclusively for music, and to show cool title at the top of ur screen.
    /// </summary>
    /// <param name="args"></param>
    private void OnPlayerSpawn(LocalPlayerAttachedEvent args)
    {
        EntityUid uid = args.Entity;

        if (TerminatingOrDeleted(uid))
            return;

        var parentStation = Transform(uid).GridUid;

        if (parentStation == null)
            return;

        // HULLROT EDIT: BoringStations and keeping track of what we've visited before is removed
        // because we want people to see the message each time you enter, coupled with music and flavor text

        var name = "placeholder";

        if (TryComp<MetaDataComponent>(parentStation, out var metadata))
            name = metadata.EntityName;

        var description = ""; //fallback for description is nothin'
        if (TryComp<VesselInfoComponent>(parentStation, out var vesselinfo))
            description = vesselinfo.Description;

        var musicPrototype = "";

        if (TryComp<VesselMusicComponent>(parentStation, out var music)) //if this succeeds, we have custom music! if it fails,
            musicPrototype = music.AmbientMusicPrototype;                                   //the component is missing and we just keep ""

        Timer.Spawn(TimeSpan.FromSeconds(10), () =>
        {
            NewVesselEnteredMessage message = new NewVesselEnteredMessage(name, description, musicPrototype);
            RaiseLocalEvent(uid, ref message, true);
        });
    }

    private void OnParentChanged(EntityUid uid, SpaceBiomeTrackerComponent component, EntParentChangedMessage args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (TerminatingOrDeleted(uid))
            return;

        var parentStation = Transform(uid).GridUid;

        if (parentStation == null) //entered space, should tell music system to stop playing ship music
        {
            var spaceMsg = new SpaceEnteredMessage();
            RaiseLocalEvent(uid, ref spaceMsg, true);
            return;
        }

        // HULLROT EDIT: BoringStations and keeping track of what we've visited before is removed
        // because we want people to see the message each time you enter, coupled with music and flavor text

        var name = MetaData((EntityUid)parentStation).EntityName;

        var description = ""; //fallback to "" in case we have none

        if (TryComp<VesselInfoComponent>(parentStation, out var desc))
            description = desc.Description;

        var musicPrototype = "";

        if (TryComp<VesselMusicComponent>(parentStation, out var music)) //if this succeeds, we have custom music! if it fails,
            musicPrototype = music.AmbientMusicPrototype;                                   //the component is missing and we just keep ""

        NewVesselEnteredMessage message = new NewVesselEnteredMessage(name, description, musicPrototype);
        RaiseLocalEvent(uid, ref message, true);
    }

    public void AddBiome(EntityUid uid, SpaceBiomeSourceComponent source)
    {
        foreach (Vector2 chunkPos in GetCoveredChunks(_formSys.GetWorldPosition(uid), source.SwapDistance))
        {
            if (!_chunks.ContainsKey(chunkPos))
                _chunks[chunkPos] = new();
            _chunks[chunkPos].Add(uid);
        }
    }

    //works assuming that biome source position and range haven't changed
    public void RemoveBiome(EntityUid uid, SpaceBiomeSourceComponent source)
    {
        foreach (Vector2 chunkPos in GetCoveredChunks(_formSys.GetWorldPosition(uid), source.SwapDistance))
        {
            if (_chunks.ContainsKey(chunkPos))
            {
                if (_chunks[chunkPos].Count == 1)
                {
                    _chunks.Remove(chunkPos);
                    continue;
                }
                _chunks[chunkPos].Remove(uid);
            }
        }
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

    private List<Vector2> GetCoveredChunks(Vector2 pos, int radius)
    {
        List<Vector2> result = new();
        Vector2 posFloor = (pos / ChunkSize).Floored() * ChunkSize;

        int chunks = (radius + ChunkSize - 1) / ChunkSize; //ceil of int division
        for (int y = -chunks; y <= chunks; y++)
        {
            for (int x = -chunks; x <= chunks; x++)
            {
                Vector2 chunkPos = new Vector2(x * ChunkSize, y * ChunkSize) + posFloor;
                if (RectCircleIntersect(
                    new Box2(chunkPos, chunkPos + new Vector2(ChunkSize)),
                    pos,
                    radius))
                {
                    result.Add(chunkPos);
                }
            }
        }

        return result;
    }

    public void RegenerateChunks()
    {
        _chunks.Clear();
        var query = EntityQueryEnumerator<SpaceBiomeSourceComponent>();

        while (query.MoveNext(out var uid, out var source))
        {
            AddBiome(uid, source);
        }
    }
    private static bool RectCircleIntersect(Box2 rect, Vector2 circPos, float circRadius)
    {
        Vector2 delta = circPos - rect.Center;

        if (delta.X > rect.Width / 2 + circRadius || delta.Y > rect.Height / 2 + circRadius)
            return false;

        if (delta.X < rect.Width / 2 || delta.Y < rect.Height / 2)
            return true;

        delta.X -= rect.Width / 2;
        delta.Y -= rect.Height / 2;

        return delta.Length() < circRadius;
    }
}
