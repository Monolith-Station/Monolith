using Content.Server.Actions;
using Content.Server.Teleportation;
using Content.Shared._Mono.Teleportation;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Teleportation;

namespace Content.Server._Mono.Teleportation;

public sealed class ScramActionSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TeleportSystem _teleportSys = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScrammerComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<ScrammerComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ScrammerComponent, ScrammerScramEvent>(OnScram);

        SubscribeLocalEvent<ScrammerComponent, ItemToggledEvent>(OnToggled);
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
        if (!ent.Comp.Enabled)
        {
            _popup.PopupEntity(Loc.GetString("action-scram-popup-disabled"), ent, ent);
            return;
        }

        _teleportSys.RandomTeleport(ent, ent.Comp.Specifier);
        args.Handled = true;
    }

    // if we're something capable of being turned on/off, respect it
    private void OnToggled(Entity<ScrammerComponent> ent, ref ItemToggledEvent args)
    {
        if (ent.Comp.ItemToggleToggle)
            ent.Comp.Enabled = args.Activated;
    }
}
