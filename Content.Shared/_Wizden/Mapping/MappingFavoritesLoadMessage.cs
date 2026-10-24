/// Reserve - File heavily edited by PR: Mapping editor.
/// See https://github.com/space-wizards/space-station-14/pull/34302
/// https://github.com/Monolith-Station/Monolith/pull/3810
/// and https://github.com/Reserve-Station/Reserve-Station/pull/82 for more details.

using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Mapping;

public sealed class MappingFavoritesLoadMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;
    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableUnordered;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
    }
}
