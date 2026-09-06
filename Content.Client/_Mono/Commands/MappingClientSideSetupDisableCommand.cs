//Reserve - Wizden mapping editor
using Content.Client.Actions;
using Content.Client.Mapping;
using Content.Client.Markers;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.State;
using Robust.Shared.Console;

namespace Content.Client.Commands;

[UsedImplicitly]
internal sealed partial class MappingClientSideSetupDisableCommand : LocalizedCommands
{
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private ILightManager _lightManager = default!;

    public override string Command => "mappingclientsidesetupdisable";

    public override string Help => LocalizationManager.GetString($"cmd-{Command}-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_lightManager.LockConsoleAccess)
        {
            _entitySystemManager.GetEntitySystem<MarkerSystem>().MarkersVisible = false;
            _lightManager.Enabled = true;
            shell.ExecuteCommand("zoom 1");
            shell.ExecuteCommand("scene GameplayState");
        }
    }
}

