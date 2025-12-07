using Content.Server._NF.CryoSleep;
using Content.Server.Chat.Managers;
using Content.Server.Roles;
using Content.Shared.GameTicking;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Map;

namespace Content.Server._Mono.SpawnCoordinates;

/// <summary>
/// System that displays spawn coordinates to players when they spawn.
/// </summary>
public sealed class SpawnCoordinatesSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<JobRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private MapCoordinates? GetSpawnCoordinates(EntityUid ent)
    {
        if (!TryComp<PlayerJobComponent>(ent, out var comp))
            return null;

        comp.SpawnCoordinates ??= _transform.GetMapCoordinates(ent);
        return comp.SpawnCoordinates.Value;
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        // Get the coordinates
        var coordinates = GetSpawnCoordinates(ev.Mob);

        if (coordinates is null)
            return;

        // Get the message
        var message = Loc.GetString("spawn-coordinates", ("x", (int)coordinates.Value.X), ("y", (int)coordinates.Value.Y));

        // Send server message
        _chatManager.DispatchServerMessage(ev.Player, message);
    }

    private void OnGetBriefing(Entity<JobRoleComponent> ent, ref GetBriefingEvent args)
    {
        if(args.Mind.Comp.OwnedEntity is not { } uid)
            return;

        // Get the coordinates
        var coordinates = GetSpawnCoordinates(uid);

        if (coordinates is null)
            return;

        // Get the message
        var message = Loc.GetString("spawn-coordinates", ("x", (int)coordinates.Value.X), ("y", (int)coordinates.Value.Y));

        // Add it to the briefing
        args.Append(message);
    }
}
