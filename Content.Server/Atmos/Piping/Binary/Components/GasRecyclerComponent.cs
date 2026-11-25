using Content.Shared.Atmos;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Atmos.Piping.Binary.Components
{
    [RegisterComponent]
    public sealed partial class GasRecyclerComponent : Component
    {
        [ViewVariables(VVAccess.ReadOnly)]
        [DataField("reacting")]
        public Boolean Reacting { get; set; } = false;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("inlet")]
        public string InletName { get; set; } = "inlet";

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("outlet")]
        public string OutletName { get; set; } = "outlet";

        [ViewVariables(VVAccess.ReadWrite)]
        public float MinTemp = 300 + Atmospherics.T0C;

        [DataField("BaseMinTemp")]
        public float BaseMinTemp = 300 + Atmospherics.T0C;

        [DataField]
        public ProtoId<MachinePartPrototype> MachinePartMinTemp = "Capacitor";

        [DataField("partRatingMinTempMultiplier")]
        public float PartRatingMinTempMultiplier = 0.95f;

        [ViewVariables(VVAccess.ReadWrite)]
        public float MinPressure = 30 * Atmospherics.OneAtmosphere;

        [DataField("BaseMinPressure")]
        public float BaseMinPressure = 30 * Atmospherics.OneAtmosphere;

        [DataField]
        public ProtoId<MachinePartPrototype> MachinePartMinPressure = "Manipulator";

        [DataField("partRatingMinPressureMultiplier")]
        public float PartRatingMinPressureMultiplier = 0.8f;
    }
}
