using Content.Server.EUI;
using Content.Shared._Mono.Persistence;
using Content.Shared.Eui;

namespace Content.Server._Mono.Persistence;

public sealed class CryopodSpawnEui(CryopodSpawnEuiState state, Action<Guid?> choose) : BaseEui
{
    public override void Opened() => StateDirty();
    public override EuiStateBase GetNewState() => state;

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is CryopodSpawnChoice choice)
        {
            Close();
            choose(choice.Id);
        }
        else
            base.HandleMessage(msg);
    }
}
