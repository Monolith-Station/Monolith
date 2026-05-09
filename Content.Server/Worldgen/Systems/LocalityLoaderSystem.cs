using Content.Server.Worldgen.Components;
using Robust.Server.GameObjects;
using Content.Server._NF.Worldgen.Components.Debris; // Frontier
using Content.Shared.Humanoid; // Frontier
using Content.Shared.Mobs.Components; // Frontier
using System.Numerics;
using Content.Server._Mono.Worldgen.Components; // Frontier
using Robust.Shared.Map; // Frontier
using Content.Server._NF.Salvage; // Frontier

using EntityPosition = (Robust.Shared.GameObjects.EntityUid Entity, Robust.Shared.Map.EntityCoordinates Coordinates);
using Content.Server.StationEvents.Events; // Frontier

namespace Content.Server.Worldgen.Systems;

/// <summary>
///     This handles loading in objects based on distance from player, using some metadata on chunks.
/// </summary>
public sealed class LocalityLoaderSystem : BaseWorldSystem
{
    [Dependency] private readonly TransformSystem _xformSys = default!;
    [Dependency] private readonly LinkedLifecycleGridSystem _linkedLifecycleGrid = default!;
    private EntityQuery<LoadedChunkComponent> _loadedQuery;
    private EntityQuery<WorldControllerComponent> _controllerQuery;
    private EntityQuery<ChunkLoaderComponent> _chunkLoaderQuery;

    public override void Initialize()
    {
        SubscribeLocalEvent<SpaceDebrisComponent, EntityTerminatingEvent>(OnDebrisDespawn);

        _loadedQuery = GetEntityQuery<LoadedChunkComponent>(); // Mono: Cached Queries
        _controllerQuery = GetEntityQuery<WorldControllerComponent>(); // Mono
        _chunkLoaderQuery = GetEntityQuery<ChunkLoaderComponent>(); // Mono
        TransformQuery = GetEntityQuery<TransformComponent>(); // Mono
    }
    // Frontier

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        var e = EntityQueryEnumerator<LocalityLoaderComponent, TransformComponent>();

        while (e.MoveNext(out var uid, out var loadable, out var xform))
        {
            if (!_controllerQuery.TryComp(xform.MapUid, out var controller)) // Mono
            {
                RaiseLocalEvent(uid, new LocalStructureLoadedEvent());
                RemCompDeferred<LocalityLoaderComponent>(uid);
                continue;
            }

            var coords = GetChunkCoords(uid, xform);
            var done = false;
            var worldPos = _xformSys.GetWorldPosition(xform); // Mono
            for (var i = -1; i < 2 && !done; i++)
            {
                for (var j = -1; j < 2 && !done; j++)
                {
                    var chunk = GetOrCreateChunk(coords + (i, j), xform.MapUid!.Value, controller);
                    if (!_loadedQuery.TryGetComponent(chunk, out var loaded) || loaded.Loaders is null)
                        continue;

                    foreach (var loader in loaded.Loaders)
                    {
                        // Mono edit start
                        var distance = loadable.LoadingDistance;

                        if (_chunkLoaderQuery.TryComp(loader, out var cLoad)) // Mono
                            distance = cLoad.LoadingDistance;

                        if (!TransformQuery.TryComp(loader, out var loaderXform)) // Mono
                            continue;

                        if ((_xformSys.GetWorldPosition(loaderXform) - worldPos).LengthSquared() > distance * distance) // Mono - use LengthSquared
                            continue;
                        // Mono edit end
                        RaiseLocalEvent(uid, new LocalStructureLoadedEvent());
                        RemCompDeferred<LocalityLoaderComponent>(uid);
                        done = true;
                        break;
                    }
                }
            }
        }
    }

    // Frontier
    private void OnDebrisDespawn(EntityUid entity, SpaceDebrisComponent component, EntityTerminatingEvent e)
    {
        if (entity != null)
        {
            // Handle mobrestrictions getting deleted
            var query = AllEntityQuery<NFSalvageMobRestrictionsComponent>();

            while (query.MoveNext(out var salvUid, out var salvMob))
            {
                if (entity == salvMob.LinkedGridEntity && salvMob.DespawnIfOffLinkedGrid) // Mono - fix
                {
                    QueueDel(salvUid);
                }
            }

            // Do not delete the grid, it is being deleted.
            _linkedLifecycleGrid.UnparentPlayersFromGrid(grid: entity, deleteGrid: false, ignoreLifeStage: true);
        }
    }
    // Frontier
}

/// <summary>
///     A directed fired on a loadable entity when a local loader enters it's vicinity.
/// </summary>
public record struct LocalStructureLoadedEvent;
