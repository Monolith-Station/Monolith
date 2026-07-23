/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Collections.Generic;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Client.Graphics;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;

namespace Content.Client._CE.ZLevels.Core.Overlays;

/// <summary>
/// Debug aid for the terrain wall collision: for every grid on a z-level, draws the hull tiles the
/// collision code considers to be sitting on a wall (yellow outline) and the terrain wall tiles they
/// have driven into (red fill). This is the exact geometry
/// <c>CEZLevelsSystem.ResolveWallCollision</c> pushes the hull out of — if a ship is being blocked
/// where there is no wall, this shows what it thinks it is hitting. Toggle with <c>showwallcollision</c>.
/// </summary>
public sealed class CEZWallCollisionOverlay : Overlay
{
    [Dependency] private IEntityManager _entityManager = null!;

    private readonly CEClientZLevelsSystem _zLevels;
    private readonly List<CESharedZLevelsSystem.CEWallContact> _contacts = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public CEZWallCollisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _zLevels = _entityManager.System<CEClientZLevelsSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        // GetWallContacts returns nothing for the terrain grids and off-network grids, so a plain
        // grid sweep is enough — no need to pre-filter to z-levels here.
        var query = _entityManager.EntityQueryEnumerator<MapGridComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _zLevels.GetWallContacts(uid, _contacts);

            foreach (var contact in _contacts)
            {
                handle.DrawRect(contact.WallTile, Color.Red.WithAlpha(0.35f));
                handle.DrawRect(contact.ShipTile, Color.Yellow.WithAlpha(0.8f), filled: false);
            }
        }
    }
}

public sealed class CEShowWallCollisionCommand : LocalizedCommands
{
    [Dependency] private IOverlayManager _overlayManager = null!;

    public override string Command => "showwallcollision";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_overlayManager.HasOverlay<CEZWallCollisionOverlay>())
        {
            _overlayManager.RemoveOverlay<CEZWallCollisionOverlay>();
            return;
        }

        _overlayManager.AddOverlay(new CEZWallCollisionOverlay());
    }
}
