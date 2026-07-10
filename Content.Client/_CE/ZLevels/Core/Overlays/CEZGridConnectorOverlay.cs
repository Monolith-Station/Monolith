/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Client.Graphics;
using Robust.Shared.Console;
using Robust.Shared.Enums;

namespace Content.Client._CE.ZLevels.Core.Overlays;

/// <summary>
/// Shitcode mapping aid: draws a circle at every grid connector's world position — the
/// exact point <c>CEZGridConnectorSystem</c> tests for a tile on the layer above — so you
/// can line the upper grid up over its connectors. All z-maps share one world coordinate
/// space, so a connector on the layer below draws at the same spot in the layer you're
/// editing. Toggle with <c>showgridconnectors</c>.
/// </summary>
public sealed class CEZGridConnectorOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = null!;

    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public CEZGridConnectorOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entityManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        var query = _entityManager.EntityQueryEnumerator<CEZGridConnectorComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            var worldPos = _transform.GetWorldPosition(xform);

            // Outline ring = the tile the connector wants to bind to on the layer above.
            handle.DrawCircle(worldPos, 0.45f, Color.Lime.WithAlpha(0.4f), filled: false);
            // Filled dot = the exact checked point.
            handle.DrawCircle(worldPos, 0.1f, Color.Lime);
        }
    }
}

public sealed class CEShowGridConnectorsCommand : LocalizedCommands
{
    [Dependency] private readonly IOverlayManager _overlayManager = null!;

    public override string Command => "showgridconnectors";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_overlayManager.HasOverlay<CEZGridConnectorOverlay>())
        {
            _overlayManager.RemoveOverlay<CEZGridConnectorOverlay>();
            return;
        }

        _overlayManager.AddOverlay(new CEZGridConnectorOverlay());
    }
}
