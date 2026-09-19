using Content.Shared.DeviceLinking;
using Jint.Native;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.Components;

/// <summary>
/// Handles the execution of JavaScript commands (see jint part of <see cref="PacketModule"/>).
/// Can't work without <see cref="PacketNetworkComponent"/>, Frequencies 0-10 are reserved for executors.
/// </summary>
[RegisterComponent]
public sealed partial class PacketExecutorComponent : Component
{
    /// <summary>
    /// Modules that will be loaded when engine initializes.
    /// <see cref="PacketModule"/>
    /// </summary>
    [DataField]
    public List<string> Modules = new();

    /// <summary>
    /// Next message(s) to be logged. Logged message(s) are checked every tick.
    /// </summary>
    [DataField]
    public string Log = "";

    /// <summary>
    /// Currently saved command for next execution(s).
    /// </summary>
    [DataField]
    public string Command = "";

    /// <summary>
    /// How much time should person wait between executions?
    /// </summary>
    [DataField]
    public TimeSpan ExecutionCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan CurrentCooldown = TimeSpan.Zero;

    /// <summary>
    /// Maximum amount of characters command can have to be executed or saved.
    /// </summary>
    [DataField]
    public int MaxCommandLength = 6500;

    /// <summary>
    /// Sound that will be played if execution attempt fails (Due to cooldown or code being too long).
    /// </summary>
    [DataField]
    public SoundSpecifier? ExecutionFailSound;


    /// <summary>
    /// Maximum amount of memory that can be used each execution.
    /// </summary>
    [DataField]
    public int MemoryAllocation = 1024 * 128; // 128KB

    /// <summary>
    /// Maximum amount of statements per execution
    /// </summary>
    [DataField]
    public int MaximumExecutionStatements = 350;

    /// <summary>
    /// Name for sink ports. You should create prototypes for those based on PortCount (SignalPortNaming+PortNumber)
    /// </summary>
    [DataField]
    public string SignalPortNaming = "ExecutorPort";

    /// <summary>
    /// How much sink ports can executor use?
    /// </summary>
    [DataField]
    public int PortCount = 5;

    /// <summary>
    /// Ports that executor currently listens to. Contains JS function.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SinkPortPrototype>, JsValue> ListeningPorts = [];

    /// <summary>
    /// This is responsible for debug logging (<see cref="PacketModule"/> & other future features.
    /// </summary>
    [DataField]
    public bool DebugState;
}
