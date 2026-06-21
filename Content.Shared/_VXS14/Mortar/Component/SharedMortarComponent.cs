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

        [ViewVariables(VVAccess.ReadWrite), DataField("accuracy"), AutoNetworkedField]
        public float BaseAccuracy = 1f;

        // Accuracy degradation per tile distance (higher values = faster degradation)
        [ViewVariables(VVAccess.ReadWrite), DataField("accuracyDegradation"), AutoNetworkedField]
        public float AccuracyDegradation = 0.05f;

        // Min and max offset range for X and Y coordinates
        [ViewVariables(VVAccess.ReadWrite), DataField("minOffsetX"), AutoNetworkedField]
        public float MinOffsetX = -10f;

        [ViewVariables(VVAccess.ReadWrite), DataField("maxOffsetX"), AutoNetworkedField]
        public float MaxOffsetX = 50f;

        [ViewVariables(VVAccess.ReadWrite), DataField("minOffsetY"), AutoNetworkedField]
        public float MinOffsetY = -10f;

        [ViewVariables(VVAccess.ReadWrite), DataField("maxOffsetY"), AutoNetworkedField]
        public float MaxOffsetY = 50f;

        // Minimum safe distance to prevent self-damage (in tiles)
        [ViewVariables(VVAccess.ReadWrite), DataField("minSafeDistance"), AutoNetworkedField]
        public float MinSafeDistance = 5f;

        // Stored target offset for auto-fire on shell insert
        [ViewVariables(VVAccess.ReadWrite), DataField("targetOffsetX"), AutoNetworkedField]
        public float TargetOffsetX = 5f;

        [ViewVariables(VVAccess.ReadWrite), DataField("targetOffsetY"), AutoNetworkedField]
        public float TargetOffsetY = 0f;

        // Time the player must hold still after inserting a shell before it fires (controls rate of fire)
        [ViewVariables(VVAccess.ReadWrite), DataField("loadDelay"), AutoNetworkedField]
        public TimeSpan LoadDelay = TimeSpan.FromSeconds(2);
    }
}
