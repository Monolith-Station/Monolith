using Content.Server.Teleportation;
using Content.Server.Actions;
using Content.Shared._Mono.Teleportation;
using Content.Shared.Teleportation;

namespace Content.Server._Mono.Teleportation;

public sealed class ScramActionSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly TeleportSystem _teleportSys = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScrammerComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<ScrammerComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ScrammerComponent, ScrammerScramEvent>(OnScram);
    }

    private void OnInit(Entity<ScrammerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ActionUid =_action.AddAction(ent, ent.Comp.ActionProto);
    }

    private void OnRemove(Entity<ScrammerComponent> ent, ref ComponentRemove args)
    {
        _action.RemoveAction(ent, ent.Comp.ActionUid);
    }

    private void OnScram(Entity<ScrammerComponent> ent, ref ScrammerScramEvent args)
    {
        _teleportSys.RandomTeleport(ent, ent.Comp.Specifier);
        args.Handled = true;
    }
}
