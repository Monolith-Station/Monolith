using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Shipyard;

public sealed class SharedPreview
{
    [Serializable, NetSerializable]
    public enum ShipyardPreviewUiKey : byte
    {
        Key
    }

    [Serializable, NetSerializable]
    public sealed class ShipyardPreviewUserInterfaceState : BoundUserInterfaceState
    {
        public int GunneryPointsUsed;
        public int GunneryPointsMax;

        public ShipyardPreviewUserInterfaceState(int gunneryPointsUsed, int gunneryPointsMax)
        {
            GunneryPointsMax = gunneryPointsMax;
            GunneryPointsUsed = gunneryPointsUsed;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ShipyardPreviewExitMessage : BoundUserInterfaceMessage
    {

    }
}
