
using Content.Server.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Presets
{
    /// <summary>
    ///     A round-start setup preset, such as which antagonists to spawn.
    /// </summary>
    [Prototype]
    public sealed partial class GamePresetPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField("alias")]
        public string[] Alias = Array.Empty<string>();

        [DataField("name")]
        public string ModeTitle = "????";

        [DataField("description")]
        public string Description = string.Empty;

        [DataField("showInVote")]
        public bool ShowInVote;

        [DataField("minPlayers")]
        public int? MinPlayers;

        [DataField("maxPlayers")]
        public int? MaxPlayers;

        [DataField]
        public List<EntProtoId> Rules { get; private set; } = new();

        /// <summary>
        /// If specified, the gamemode will only be run with these maps.
        /// If none are elligible, the global fallback will be used.
        /// </summary>
        [DataField("supportedMaps")]
        public ProtoId<GameMapPoolPrototype>? MapPool;

        /// <summary>
        /// Mono - as how much votes should each player click count.
        /// </summary>
        [DataField]
        public float Weight = 1f;

        /// <summary>
        /// Mono - multiply respawn timer by this amount
        /// </summary>
        [DataField]
        public float RespawnMultiplier = 1f;

        /// <summary>
        /// Mono - enable the autobalancer for TSF/PDV
        /// </summary>
        [DataField]
        public bool AutobalancerEnabled = false;
    }
}
