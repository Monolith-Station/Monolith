using Content.Shared._Mono.Persistence;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Planets;

public sealed partial class PersistenceClipboardSystem : EntitySystem
{
    [Dependency] private IClipboardManager _clipboard = default!;

    public override void Initialize() =>
        SubscribeNetworkEvent<PersistenceClipboardYaml>(message => _clipboard.SetText(message.Yaml)); // HOW I WISH THERE WAS ANY SORT OF FUCKING API FOR THIS. RAGH.
}
