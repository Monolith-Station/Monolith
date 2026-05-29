using Content.Client.CombatMode;
using Content.Client.Hands.Systems;
using Content.Shared._RMC14.CombatMode;
using Robust.Client.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._RMC14.CombatMode;

public sealed partial class RMCCombatModeUISystem : EntitySystem
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private CombatModeSystem _combatMode = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private RMCCombatModeSystem _rmcCombatMode = default!;

    private ICursor? _crosshairCursor;

    public override void FrameUpdate(float frameTime)
    {
        if (_combatMode.IsInCombatMode() &&
            _hands.GetActiveHandEntity() is { } held &&
            _rmcCombatMode.GetCrosshair(held) != null)
        {
            _crosshairCursor ??= _clyde.CreateCursor(new Image<Rgba32>(1, 1), Vector2i.Zero);
            _clyde.SetCursor(_crosshairCursor);
        }
        else
        {
            _clyde.SetCursor(null);
        }
    }
}
