using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.IoC;

namespace Content.Shared._VXS14.Mortar
{
    [RegisterComponent][AutoGenerateComponentState]
    public partial class SharedMortarComponent : Component
    {

        [ViewVariables(VVAccess.ReadWrite), DataField("baseAccuracy"), AutoNetworkedField]
        public float BaseAccuracy = 1f;

        [ViewVariables(VVAccess.ReadWrite), DataField("accuracyDegradation"), AutoNetworkedField]
        public float AccuracyDegradation = 0.05f;

        [ViewVariables(VVAccess.ReadWrite), DataField("maxSpread"), AutoNetworkedField]
        public float MaxSpread = 10f;

        [ViewVariables(VVAccess.ReadWrite), DataField("minOffsetX"), AutoNetworkedField]
        public float MinOffsetX = -10f;

        [ViewVariables(VVAccess.ReadWrite), DataField("maxOffsetX"), AutoNetworkedField]
        public float MaxOffsetX = 50f;

        [ViewVariables(VVAccess.ReadWrite), DataField("minOffsetY"), AutoNetworkedField]
        public float MinOffsetY = -10f;

        [ViewVariables(VVAccess.ReadWrite), DataField("maxOffsetY"), AutoNetworkedField]
        public float MaxOffsetY = 50f;

        [ViewVariables(VVAccess.ReadWrite), DataField("minSafeDistance"), AutoNetworkedField]
        public float MinSafeDistance = 5f;

        [ViewVariables(VVAccess.ReadWrite), DataField("targetOffsetX"), AutoNetworkedField]
        public float TargetOffsetX = 5f;

        [ViewVariables(VVAccess.ReadWrite), DataField("targetOffsetY"), AutoNetworkedField]
        public float TargetOffsetY = 0f;

        [ViewVariables(VVAccess.ReadWrite), DataField("loadDelay"), AutoNetworkedField]
        public TimeSpan LoadDelay = TimeSpan.FromSeconds(2);

        [ViewVariables(VVAccess.ReadWrite), DataField("currentLoader")]
        public EntityUid? CurrentLoader;
    }
}
