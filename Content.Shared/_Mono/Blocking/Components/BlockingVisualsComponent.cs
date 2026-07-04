using Content.Shared._Mono.Blocking;
using Robust.Shared.GameStates;

namespace Content.Client._Mono.Blocking.Components;

/// <summary>
/// This component gets dynamically added to an Entity via the <see cref="BlockingSystem"/> if the IsClothing is true
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedBlockingSystem))]
[AutoGenerateComponentState]
public sealed partial class BlockingVisualsComponent : Component
{
    /// <summary>
    /// Self-explanatory.
    /// </summary>
    [DataField("enabled")]
    [AutoNetworkedField]
    public bool Enabled = true;
}
