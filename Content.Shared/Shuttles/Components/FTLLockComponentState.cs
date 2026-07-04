using System;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.Components;

[Serializable, NetSerializable]
public sealed class FTLLockComponentState : ComponentState
{
    public bool Enabled;

    public FTLLockComponentState(bool enabled)
    {
        Enabled = enabled;
    }
}