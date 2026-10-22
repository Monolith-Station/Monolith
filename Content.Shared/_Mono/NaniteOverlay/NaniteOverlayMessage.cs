using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._Mono.NaniteOverlay;

[Serializable, NetSerializable]
public sealed class NaniteOverlayMessage : EntityEventArgs
{
    public NetEntity[] Targets;
    public FixedPoint2[]? Responses;

    public NaniteOverlayMessage(NetEntity[] targets)
    {
        Targets = targets;
        Responses = null;
    }

    public NaniteOverlayMessage(NetEntity[] targets, FixedPoint2[] responses)
    {
        Targets = targets;
        Responses = responses;
    }
}
