using System.Numerics;
using Content.Shared._NF.Shuttles.Events;
using Content.Shared.DeviceLinking;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

// Mono
using Robust.Shared.Audio;

namespace Content.Server.Shuttles.Components
{
    [RegisterComponent]
    [AutoGenerateComponentState]
    public sealed partial class ShuttleConsoleComponent : SharedShuttleConsoleComponent
    {
        [ViewVariables]
        public readonly List<EntityUid> SubscribedPilots = new();

        /// <summary>
        /// Custom display names for network port buttons.
        /// Key is the port ID, value is the display name.
        /// </summary>
        [DataField("portLabels"), AutoNetworkedField]
        public new Dictionary<string, string> PortNames = new();

        /// <summary>
        /// How much should the pilot's eye be zoomed by when piloting using this console?
        /// </summary>
        [DataField("zoom")]
        public Vector2 Zoom = new(1.5f, 1.5f);

        /// <summary>
        /// Should this console have access to restricted FTL destinations?
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite), DataField("whitelistSpecific")]
        public List<EntityUid> FTLWhitelist = new List<EntityUid>();

        // Frontier: EMP-related state
        /// <summary>
        /// For EMP to allow keeping the shuttle off
        /// </summary>
        [DataField("enabled")]
        public bool MainBreakerEnabled = true;

        /// <summary>
        ///     While disabled by EMP
        /// </summary>
        [DataField("timeoutFromEmp", customTypeSerializer: typeof(TimeOffsetSerializer))]
        public TimeSpan TimeoutFromEmp = TimeSpan.Zero;

        [DataField("disableDuration"), ViewVariables(VVAccess.ReadWrite)]
        public float DisableDuration = 60f;

        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public InertiaDampeningMode DampeningMode = InertiaDampeningMode.Dampen;
        // End Frontier

        // <Mono>
        [DataField]
        public string AutopilotTargetKey = "Target";

        [DataField]
        public string AutopilotRotationKey = "TargetRotation";

        [DataField]
        public SoundSpecifier? AutopilotDoneSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/radar_ping.ogg");
        // </Mono>

        // Network Port Button Source Ports
        [DataField]
        public List<ProtoId<SourcePortPrototype>> SourcePorts = new()
        {
            "device-button-1",
            "device-button-2",
            "device-button-3",
            "device-button-4",
            "device-button-5",
            "device-button-6",
            "device-button-7",
            "device-button-8"
        };
    }
}
