using Content.Server.NPC.Systems;
using Content.Shared.Whitelist;

// Mono - whole file

namespace Content.Server.NPC.Queries.Queries;

/// <summary>
/// Returns nearby shuttles considered hostile from <see cref="FactionSystem"/>
/// </summary>
public sealed partial class NearbyHostileShuttlesQuery : UtilityQuery
{
    [DataField]
    public float Range = 2000f;

    // TODO: make this use factions
    [DataField]
    public EntityWhitelist Blacklist = new();
}
