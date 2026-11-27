using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Planets;

[Serializable, NetSerializable]
public sealed class PlanetViewerEuiState(string text) : EuiStateBase
{
    public readonly string Text = text;
}

[Serializable, NetSerializable]
public sealed class PlanetViewerRefreshMessage : EuiMessageBase;
