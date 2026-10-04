using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.UserInterface;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Mining;

[RegisterComponent, NetworkedComponent]
public sealed partial class OreDepositSurfaceComponent : Component;

public sealed partial class SharedOreScannerSystem : EntitySystem
{
    [Dependency] private CESharedZLevelsSystem _levels = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminManager _admins = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OreScannerComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(Entity<OreScannerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (TryGetSurface(ent.Owner, out _))
            return;
        args.Cancel();
        _popup.PopupClient(Loc.GetString("ore-scanner-not-on-planet"), ent.Owner, args.User);
    }

    public bool TryGetSurface(EntityUid uid, out EntityUid surface)
    {
        surface = default;
        if (Transform(uid).MapUid is not { } map)
            return false;
        if (HasComp<OreDepositSurfaceComponent>(map))
        {
            surface = map;
            return true;
        }
        if (!TryComp<CEZMapComponent>(map, out var zMap))
            return false;
        foreach (var below in _levels.GetAllMapsBelow((map, zMap)))
        {
            if (!HasComp<OreDepositSurfaceComponent>(below))
                continue;
            surface = below;
            return true;
        }
        return false;
    }
}
