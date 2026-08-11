using Content.Server._Forge.Silicons.StationAi;

namespace Content.Server.Ghost.Roles;

public sealed partial class ToggleableGhostRoleSystem
{
    private bool IsStationAi(EntityUid uid)
    {
        return HasComp<StationAiPersonalityComponent>(uid);
    }
}
