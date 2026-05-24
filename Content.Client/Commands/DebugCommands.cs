using Content.Client.Markers;
using Content.Client.Popups;
using Content.Client.SubFloor;
using Content.Shared.SubFloor;
using Robust.Client.GameObjects;
using Robust.Shared.Console;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.Commands;

internal sealed partial class ShowMarkersCommand : LocalizedCommands
{
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;

    public override string Command => "showmarkers";

    public override string Help => LocalizationManager.GetString($"cmd-{Command}-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _entitySystemManager.GetEntitySystem<MarkerSystem>().MarkersVisible ^= true;
    }
}

internal sealed partial class ShowSubFloor : LocalizedCommands
{
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;

    public override string Command => "showsubfloor";

    public override string Help => LocalizationManager.GetString($"cmd-{Command}-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _entitySystemManager.GetEntitySystem<SubFloorHideSystem>().ShowAll ^= true;
    }
}

internal sealed partial class ShowSubFloorForever : LocalizedCommands
{
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;

    public const string CommandName = "showsubfloorforever";
    public override string Command => CommandName;

    public override string Help => LocalizationManager.GetString($"cmd-{Command}-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _entitySystemManager.GetEntitySystem<SubFloorHideSystem>().ShowAll = true;

        var entMan = IoCManager.Resolve<IEntityManager>();
        var components = entMan.EntityQuery<SubFloorHideComponent, SpriteComponent>(true);

        foreach (var (_, sprite) in components)
        {
            sprite.DrawDepth = (int) DrawDepth.Overlays;
        }
    }
}

internal sealed partial class NotifyCommand : LocalizedCommands
{
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;

    public override string Command => "notify";

    public override string Help => LocalizationManager.GetString($"cmd-{Command}-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var message = args[0];

        _entitySystemManager.GetEntitySystem<PopupSystem>().PopupCursor(message);
    }
}
