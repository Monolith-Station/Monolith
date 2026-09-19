using System.Threading;
using Content.Server._KS14.Packet.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking;
using Jint;

namespace Content.Server._KS14.Packet;

/// <summary>
/// This handles most of JINT operations (since some are handled in Modules subclass).
/// Responsible for execution and engine initialization
/// </summary>
public sealed partial class PacketSystem
{
    /// <summary>
    /// Active executor entities. Normally spawned entities wont have engines.
    /// Entity will receive and cache jint engine only after first execution.
    /// Packets use their own engine.
    /// </summary>
    private Dictionary<Entity<PacketExecutorComponent>, Engine> _executorEntities = new();
    private Dictionary<Engine, CancellationTokenSource> _engineCts = new();

    /// <summary>
    /// Tries to execute current code. Accounts for cooldown and max command length.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="executor"></param>
    /// <param name="actor"></param>
    public bool TryExecute(string command, Entity<PacketExecutorComponent> executor, EntityUid actor)
    {
        if (executor.Comp.CurrentCooldown > TimeSpan.Zero)
        {
            _audioSystem.PlayEntity(executor.Comp.ExecutionFailSound,actor, executor);
            return false;
        }

        if (command.Length >= executor.Comp.MaxCommandLength)
            return false;

        ExecuteCommand(command, executor);
        executor.Comp.CurrentCooldown += executor.Comp.ExecutionCooldown;

        return true;
    }

    private void ExecuteCommand(string command, Entity<PacketExecutorComponent> executor)
    {
        executor.Comp.ListeningPorts.Clear(); // Dispose ports to init them again.
        if (TryComp<PacketNetworkComponent>(executor, out var receiver))
            ReloadFrequencies((executor, receiver));

        var engine = EnsureEngine(executor);

        try
        {
            engine.ExecuteAsync(command);
        }
        catch (Exception e)
        {
            executor.Comp.Log += e.Message + '\n';
        }
    }

    private void Cancel(Entity<PacketExecutorComponent> executor)
    {
        var engine = EnsureEngine(executor);
        var cts = EnsureToken(engine);

        cts.Cancel();
    }

    /// <summary>
    /// If engine exists - Finds it and returns it.
    /// If engine doesn't exist - Creates new one, initializes modules, ports, constants, and return it.
    /// </summary>
    /// <param name="executor"></param>
    /// <returns></returns>
    private Engine EnsureEngine(Entity<PacketExecutorComponent> executor)
    {
        if (_executorEntities.TryGetValue(executor, out var exEngine))
            return exEngine;

        var cts = new CancellationTokenSource();
        var engine = new Engine(options =>
        {
            options.MaxStatements(executor.Comp.MaximumExecutionStatements);
            options.LimitMemory(executor.Comp.MemoryAllocation);
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
            options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(40);
        });

        _engineCts.Add(engine, cts);
        _executorEntities.Add(executor, engine);
        InitializeModules(executor);
        SetConstants(executor);
        LoadMethods(executor);
        InitializePorts(executor);

        return engine;
    }

    private CancellationTokenSource EnsureToken(Engine engine)
    {
        if (_engineCts.TryGetValue(engine, out var cts))
            return cts;

        cts = new CancellationTokenSource();
        _engineCts.Add(engine, cts);

        return cts;
    }

    private void SetConstants(Entity<PacketExecutorComponent> executor)
    {
        var engine = EnsureEngine(executor);

        if (TryComp<PacketNetworkComponent>(executor, out var receiver))
        {
            engine.SetValue("SELF_FREQ", GetFrequency(receiver.Frequency));
            engine.SetValue("SELF_ADD", receiver.Address);
        }
    }

    /// <summary>
    /// Re-creates engine, loading basic module + all modules from item slots.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="slotsComponent"></param>
    private void ReloadEngine(Entity<PacketExecutorComponent> ent, ItemSlotsComponent slotsComponent)
    {
        DisposeEngine(ent);
        ent.Comp.Modules.Add("BasePacketModule"); // Basic firmware.

        foreach (var moduleSlot in slotsComponent.Slots.Values)
        {
            if (!TryComp<ExecutorModuleComponent>(moduleSlot.Item, out var moduleName))
                return;

            ent.Comp.Modules.Add(moduleName.ModuleName);
        }

        EnsureEngine(ent);
    }

    private void DisposeEngine(Entity<PacketExecutorComponent> ent)
    {
        ent.Comp.Modules.Clear();
        RemComp<DeviceLinkSinkComponent>(ent);
        _modules.Remove(ent);
        _methods.Remove(ent);

        if (!_executorEntities.Remove(ent, out var engine))
            return;

        _engineCts.Remove(engine);
        engine.Dispose();
    }

    /// <summary>
    /// Declares methods in engine for them to be executable.
    /// </summary>
    /// <param name="ent"></param>
    public void LoadMethods(Entity<PacketExecutorComponent> ent)
    {
        var engine = EnsureEngine(ent);

        foreach (var moduleName in ent.Comp.Modules)
        {
            if (!TryGetMethods(ent, moduleName, out var methods))
                return;

            foreach (var method in methods)
            {
                engine.SetValue(method.Id, method.ModuleExec);
            }
        }
    }
}
