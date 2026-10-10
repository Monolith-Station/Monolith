using Content.Shared.Alert;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Atmos.Components
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentPause] // TODO: Access restriction so that other systems have to use the API to modify fire stacks
    public sealed partial class FlammableComponent : Component
    {
        /// <summary>
        /// Is the mob currently resisting being on fire
        /// (i.e. throwing themselves onto the ground to extinguish themselves)?
        /// </summary>
        [ViewVariables]
        public bool Resisting => ResistCompleteTime.HasValue;

        /// <summary>
        /// The time stamp at which the active resist will be over and the mob's flame stacks will be reduced.
        /// </summary>
        [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
        [AutoPausedField]
        public TimeSpan? ResistCompleteTime;

        /// <summary>
        /// Timestamp for the next update.
        /// </summary>
        [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
        [AutoPausedField]
        public TimeSpan NextUpdate;

        /// <summary>
        /// The time resisting being on fire will take.
        /// The mob will be paralyzed for this duration.
        /// </summary>
        [DataField]
        public TimeSpan ResistTime = TimeSpan.FromSeconds(2);

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public bool OnFire;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float FireStacks;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float MaximumFireStacks = 10f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float MinimumFireStacks = -10f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public string FlammableFixtureID = "flammable";

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float MinIgnitionTemperature = 373.15f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public bool FireSpread { get; private set; } = false;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public bool CanResistFire { get; private set; } = false;

        // ES START
        // non required damage
        [DataField(required: false)]
        // ES END
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier Damage = new(); // Empty by default, we don't want any funny NREs.

        /// <summary>
        ///     Used for the fixture created to handle passing firestacks when two flammable objects collide.
        /// </summary>
        [DataField]
        public IPhysShape FlammableCollisionShape = new PhysShapeCircle(0.35f);

        /// <summary>
        ///     Should the component be set on fire by interactions with isHot entities
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public bool AlwaysCombustible = false;

        /// <summary>
        ///     Can the component anyhow lose its FireStacks?
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public bool CanExtinguish = true;

        /// <summary>
        ///     How many firestacks should be applied to component when being set on fire?
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float FirestacksOnIgnite = 2.0f;

        /// <summary>
        /// Determines how quickly the object will fade out. With positive values, the object will flare up instead of going out.
        /// </summary>
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public float FirestackFade = -0.1f;

        [DataField]
        public ProtoId<AlertPrototype> FireAlert = "Fire";

        // ES START
        /// <summary>
        ///     This is slightly strange, but its basically to allow things which use firestacks in appearance
        ///     w/ genericvis stuff to have discrete stages, but have firestacks 'per stage'
        ///     e.g. you have 20 firestacks, visual divisor of 5, then <see cref="FireVisuals.FireStacks"/> is set to 4
        /// </summary>
        [DataField]
        public float FirestackVisualDivisor = 1.0f;

        /// <summary>
        ///     Should this entity be deleted completely on reaching 0 firestacks?
        /// </summary>
        [DataField]
        public bool DeleteOnExtinguish = false;

        /// <summary>
        ///     Use basic fire spread logic (no mass sharing, just gives some firestacks)
        ///     Will not receive any firestacks from other entities--just gives to others
        /// </summary>
        [DataField]
        public bool BasicFireSpread = false;

        /// <summary>
        ///     What % of this entities firestacks will be added to other entities, if basic spread is on.
        /// </summary>
        [DataField]
        public float BasicFireSpreadStackPercentage = 0.1f;

        /// <summary>
        ///     How much smoke will be created through this entity burning.
        /// </summary>
        [DataField]
        public float SmokeMolsReleasedPerStack = 0.01f;

        /// <summary>
        ///     Multiplier on fire energy released into the atmosphere.
        /// </summary>
        [DataField]
        public float FireEnergyMultiplier = 20f;

        /// <summary>
        ///     Max tile temperature at which this fire will stop releasing new energy into the atmosphere.
        /// </summary>
        // this is like stupidly low for a fire obviously. but i dont really want it to have much pronounced gameplay effect
        [DataField]
        public float MaxFireTemperature = Atmospherics.T0C + 130f;
        // ES END
    }
}
