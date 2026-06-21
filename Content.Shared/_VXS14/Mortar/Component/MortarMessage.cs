using Content.Shared.Eui;
using Robust.Shared.Serialization;
using Robust.Shared.Map;
using Content.Shared.Eui;
using Robust.Shared.Serialization;
using Robust.Shared.Map;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.DoAfter;

namespace Content.Shared._VXS14.Mortar;


    public static class MortarSpawnExplosionEuiMsg
    {
        [Serializable, NetSerializable]
        public sealed class MortarCords : EuiMessageBase
        {
            public MortarCords(float offsetX, float offsetY)
            {
                OffsetX = offsetX;
                OffsetY = offsetY;
            }
            public float OffsetX;
            public float OffsetY;
        }

        [Serializable, NetSerializable]
        public sealed class MortarConfig : EuiMessageBase
        {
            public MortarConfig(float minOffsetX, float maxOffsetX, float minOffsetY, float maxOffsetY, float minSafeDistance)
            {
                MinOffsetX = minOffsetX;
                MaxOffsetX = maxOffsetX;
                MinOffsetY = minOffsetY;
                MaxOffsetY = maxOffsetY;
                MinSafeDistance = minSafeDistance;
            }
            public float MinOffsetX;
            public float MaxOffsetX;
            public float MinOffsetY;
            public float MaxOffsetY;
            public float MinSafeDistance;
        }
    }

/// <summary>
/// Raised on the mortar after DoAfter completes to fire a loaded shell.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class MortarShellLoadDoAfterEvent : SimpleDoAfterEvent { }
