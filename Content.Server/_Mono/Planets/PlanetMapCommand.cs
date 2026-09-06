using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._Mono.Planets;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Mono.Planets;

[ToolshedCommand, AdminCommand(AdminFlags.Mapping)]
public sealed class PlanetMapCommand : ToolshedCommand
{
    [CommandImplementation]
    public EntityUid Create(ProtoId<PlanetMapPrototype> prototype, int? seed = null)
    {
        return GetSys<PlanetMapSystem>().Create(prototype, seed);
    }
}
