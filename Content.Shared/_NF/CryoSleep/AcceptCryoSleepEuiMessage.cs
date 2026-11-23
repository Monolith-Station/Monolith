using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._NF.CryoSleep;

[Serializable, NetSerializable]
public enum AcceptCryoUiButton
{
    Deny,
    Accept,
}

[Serializable, NetSerializable]
public sealed class AcceptCryoChoiceMessage : EuiMessageBase
{
    public readonly AcceptCryoUiButton Button;

    public AcceptCryoChoiceMessage(AcceptCryoUiButton button)
    {
        Button = button;
    }
}

[Serializable, NetSerializable]
public sealed class CryoSleepEuiState(bool savesRespawn) : EuiStateBase
{
    public readonly bool SavesRespawn = savesRespawn;
}
