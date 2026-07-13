using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.IoC;
using Robust.Shared.Audio;

namespace Content.Shared._VXS14.Mortar
{
    [RegisterComponent][AutoGenerateComponentState]
    public partial class SharedMortarShellComponent : Component
    {

        [ViewVariables(VVAccess.ReadWrite), DataField("explosionType"), AutoNetworkedField]
        public string Type = "Default";

        [ViewVariables(VVAccess.ReadWrite), DataField("totalIntensity"), AutoNetworkedField]
        public float TotalIntensity = 105f;

        [ViewVariables(VVAccess.ReadWrite), DataField("slope"), AutoNetworkedField]
        public float Slope = 200f;

        [ViewVariables(VVAccess.ReadWrite), DataField("maxTileIntensity"), AutoNetworkedField]
        public float MaxTileIntensity = 2f;

        [ViewVariables(VVAccess.ReadWrite), DataField("delayPerTile"), AutoNetworkedField]
        public float DelayPerTile = 0.1f;

        [ViewVariables(VVAccess.ReadWrite), DataField("fireSound"), AutoNetworkedField]
        public SoundSpecifier? FireSound = new SoundPathSpecifier("/Audio/Effects/explosion_small1.ogg");

        [ViewVariables(VVAccess.ReadWrite), DataField("preExplosionSound"), AutoNetworkedField]
        public SoundSpecifier? PreExplosionSound = new SoundPathSpecifier("/Audio/Effects/explosionfar.ogg");

        [ViewVariables(VVAccess.ReadWrite), DataField("insertSound"), AutoNetworkedField]
        public SoundSpecifier? InsertSound = new SoundPathSpecifier("/Audio/Effects/thunk.ogg");

        [ViewVariables(VVAccess.ReadWrite), DataField("explosionEntity")]
        public string? ExplosionEntity;

        [ViewVariables(VVAccess.ReadWrite), DataField("useDirectExplosion")]
        public bool UseDirectExplosion = true;
    }
}
