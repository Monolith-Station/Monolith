using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> NPCMaxUpdates =
        CVarDef.Create("npc.max_updates", 256); // Frontier: 128<256 - praying to clang

    public static readonly CVarDef<bool> NPCEnabled = CVarDef.Create("npc.enabled", true);

    /// <summary>
    ///     Should NPCs pathfind when steering. For debug purposes.
    /// </summary>
    public static readonly CVarDef<bool> NPCPathfinding = CVarDef.Create("npc.pathfinding", true);

    /// <summary>
    ///     Mono: Should NPCs check player distances when moving? Mostly because fuck debugging this reliably.
    /// </summary>
    public static readonly CVarDef<bool> NPCMovementCheckPlayerDistances = CVarDef.Create("npc.movement_check_player_distances", false);

    /// <summary>
    ///     Mono: Should NPCs pause (sleep) when no players are within range?
    /// </summary>
    public static readonly CVarDef<bool> NPCPauseWhenNoPlayersInRange = CVarDef.Create("npc.pause_when_no_players_in_range", true);

    /// <summary>
    ///     Mono: Distance threshold for pausing NPCs when no players are in range.
    /// </summary>
    public static readonly CVarDef<float> NPCPlayerPauseDistance = CVarDef.Create("npc.player_pause_distance", 2000f);
}
