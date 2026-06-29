using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Lidgren.Network;

namespace Content.Shared._Mono.MonoCoins;

/// <summary>
/// Sent from the server to client to message updated MonoCoins balance.
/// </summary>
public sealed class MsgMonoCoins : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public long Coins;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Coins = buffer.ReadVariableInt64();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt64(Coins);
    }
}

public sealed class MsgMonoCoinsRequest : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
    }
}
