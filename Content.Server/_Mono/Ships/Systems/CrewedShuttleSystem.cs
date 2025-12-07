using Content.Server.Shuttles.Components;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.Ships.Components;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;

namespace Content.Server._Mono.Ships.Systems;

/// <summary>
/// This handles ensuring a crewed shuttle is only piloted and gunned by two separate people.
/// </summary>
public sealed class CrewedShuttleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    public bool AnyConsoleActiveByPlayer<T>(Entity<CrewedShuttleComponent?> shuttle, Enum key, EntityUid actor)  where T : IComponent
    {
        if (!Resolve(shuttle.Owner, ref shuttle.Comp, false))
            return false;

        var query = EntityQueryEnumerator<T>();

        while (query.MoveNext(out var uid, out _))
        {
            if (Transform(uid).ParentUid != shuttle.Owner)
                continue;

            if (!TryComp<UserInterfaceComponent>(uid, out var ui))
                continue;

            var result = _ui.IsUiOpen((uid, ui), key, actor);

            if (result)
                return true;
        }

        return false;
    }

    public bool AnyGunneryConsoleActiveByPlayer(Entity<CrewedShuttleComponent?> shuttle, EntityUid actor)
    {
        return AnyConsoleActiveByPlayer<FireControlConsoleComponent>(shuttle, FireControlConsoleUiKey.Key, actor);
    }

    public bool AnyShuttleConsoleActiveByPlayer(Entity<CrewedShuttleComponent?> shuttle, EntityUid actor)
    {
        return AnyConsoleActiveByPlayer<ShuttleConsoleComponent>(shuttle, ShuttleConsoleUiKey.Key, actor);
    }
}
