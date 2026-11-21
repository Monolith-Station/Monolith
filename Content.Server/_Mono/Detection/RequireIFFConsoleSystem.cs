using Content.Server.Shuttles.Components;
using Content.Shared.Destructible;
using Content.Shared.Shuttles.Systems;

namespace Content.Server._Mono.Detection;

public sealed partial class RequireIFFConsoleSystem : EntitySystem
{
    [Dependency] private SharedShuttleSystem _shuttle = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IFFConsoleComponent, DestructionEventArgs>(OnDestroyed);
    }
    private void OnDestroyed(Entity<IFFConsoleComponent> ent, ref DestructionEventArgs args)
    {
        if (!TryComp<RequireIFFConsoleComponent>(Transform(ent).ParentUid, out var requireConsoleComp))
            return;
        var gridUid = Transform(ent).GridUid;
        if (gridUid == null)
            return;
        _shuttle.RemoveIFFFlag(gridUid.Value, requireConsoleComp.RemoveFlags);
    }
}