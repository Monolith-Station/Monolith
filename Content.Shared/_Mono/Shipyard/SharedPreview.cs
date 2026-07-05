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

    }

    [Serializable, NetSerializable]
    public sealed class ShipyardPreviewExitMessage : BoundUserInterfaceMessage
    {

    }
}
