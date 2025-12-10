using Content.Shared.DeviceLinking;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Mono.AmmoLoader;

/// <summary>
/// Loading system that can transfer ammunition to linked ship artillery.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AmmoLoaderComponent : Component
{
    public const string ContainerId = "ammo_loader";

    /// <summary>
    /// Container holding the ammo to be loaded.
    /// </summary>
    [ViewVariables]
    public Container Container = default!;

    /// <summary>
    /// Whether the loader is currently engaged for flushing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Engaged;

    /// <summary>
    /// Source port for sending load signals to linked artillery.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<SourcePortPrototype>))]
    public string LoadPort = "AmmoLoaderLoad";

    /// <summary>
    /// Maximum number of network connections allowed on this ammo loader. Connections beyond this limit will be rejected.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxConnections = 1;

    /// <summary>
    /// Maximum number of items that can be stored in the loader's container.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxCapacity = 1;
}

