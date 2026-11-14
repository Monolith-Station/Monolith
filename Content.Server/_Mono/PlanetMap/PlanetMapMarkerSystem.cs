using Content.Shared._Mono.PlanetMap;
using Robust.Server.GameStates;

namespace Content.Server._Mono.PlanetMap;

/// <summary>
/// Sends the planet map markers to every client regardless of range, so planet views can show them anywhere.
/// </summary>
public sealed class PlanetMapMarkerSystem : EntitySystem
{
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlanetMapMarkerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PlanetMapMarkerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<PlanetMapMarkerComponent> ent, ref ComponentStartup args)
    {
        _pvsOverride.AddGlobalOverride(ent);
    }

    private void OnShutdown(Entity<PlanetMapMarkerComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            _pvsOverride.RemoveGlobalOverride(ent);
    }
}
