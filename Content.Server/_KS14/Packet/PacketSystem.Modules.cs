using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._KS14.Packet.Components;
using Content.Server._KS14.Packet.Modules;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking;

namespace Content.Server._KS14.Packet;

/// <summary>
/// This handles initialization of both <see cref="PacketModule"/> ad <see cref="ModuleMethod"/>
/// </summary>
public sealed partial class PacketSystem
{
    /// <summary>
    /// Stores dictionary of <see cref="PacketModule"/> for each executor.
    /// Even though creating PacketModule instances for each executor might be expensive, this is required for
    /// module to be able to directly access the executor.
    /// </summary>
    private Dictionary<Entity<PacketExecutorComponent>, Dictionary<string, PacketModule>> _modules = new();

    /// <summary>
    ///Stores dictionary, consisting of module ID and list of <see cref="ModuleMethod"/> for each executor.
    /// </summary>
    private Dictionary<Entity<PacketExecutorComponent>, Dictionary<string, List<ModuleMethod>>> _methods = new();

    /// <summary>
    /// Stores types for further initialization.
    /// </summary>
    private List<Type> _moduleTypes = [];
    private List<Type> _methodTypes = [];

    /// <summary>
    /// Loads <see cref="PacketModule"/> and <see cref="ModuleMethod"/> types into lists upon system initialization.
    /// </summary>
    private void PreInitJint()
    {
        _moduleTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(PacketModule).IsAssignableFrom(type)
                           && type.IsClass
                           && !type.IsAbstract)
            .ToList();

        _methodTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(ModuleMethod).IsAssignableFrom(type)
                           && type.IsClass
                           && !type.IsAbstract)
            .ToList();
    }

    /// <summary>
    /// Loads available modules into dictionary. Checks if executor has this module in <see cref="PacketExecutorComponent"/>
    /// Additionally loads <see cref="PacketNetworkComponent"/> into module if entity has one
    /// </summary>
    /// <param name="ent"></param>
    private void InitializeModules(Entity<PacketExecutorComponent> ent)
    {
        Dictionary<string, PacketModule> modDict = [];

        foreach (var module in _moduleTypes)
        {
            if (!ent.Comp.Modules.Contains(module.Name))
                continue;

            object[] args = [EntityManager, _prototypeManager, this];
            if (Activator.CreateInstance(module, args) is not PacketModule moduleInstance)
                continue;

            moduleInstance.Executor = ent;
            if (TryComp<PacketNetworkComponent>(moduleInstance.Executor, out var packetNetwork))
                moduleInstance.Executor.Comp2 = packetNetwork;

            modDict.Add(module.Name, moduleInstance);
        }
        _modules.Add(ent, modDict);

        InitializeMethods(ent);
    }

    /// <summary>
    /// Loads available module methods into dictionary.
    /// </summary>
    /// <param name="ent"></param>
    private void InitializeMethods(Entity<PacketExecutorComponent> ent)
    {
        Dictionary<string, List<ModuleMethod>> methodDict = [];

        foreach (var method in _methodTypes)
        {
            if (Attribute.GetCustomAttribute(method, typeof(ModuleMethodAttribute)) is not ModuleMethodAttribute methodData
                || !ent.Comp.Modules.Contains($"{methodData.Method}")) // Checks if executor has module for this method.
                continue;

            if (!TryGetModule(ent, $"{methodData.Method}", out var module)
                || Activator.CreateInstance(method, module) is not ModuleMethod methodInstance)
                continue;

            if (!methodDict.ContainsKey($"{methodData.Method}"))
                methodDict.Add($"{methodData.Method}", [methodInstance]);
            else
                methodDict[$"{methodData.Method}"].Add(methodInstance);
        }

        _methods.Add(ent, methodDict);
    }

    /// <summary>
    /// Tries to find <see cref="PacketModule"/> by module name.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="moduleName"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    public bool TryGetModule(Entity<PacketExecutorComponent> ent, string moduleName, [NotNullWhen(returnValue: true)] out PacketModule? module)
    {
        module = null;

        if (!_modules.TryGetValue(ent, out var moduleDict))
            return false;

        return moduleDict.TryGetValue(moduleName, out module);
    }

    /// <summary>
    /// Tries to get all <see cref="ModuleMethod"/> by module name.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="moduleName"></param>
    /// <param name="methods"></param>
    /// <returns></returns>
    public bool TryGetMethods(Entity<PacketExecutorComponent> ent, string moduleName, [NotNullWhen(returnValue: true)] out List<ModuleMethod>? methods)
    {
        methods = [];

        if (!_methods.TryGetValue(ent, out var moduleDict))
            return false;

        return moduleDict.TryGetValue(moduleName, out methods);
    }

    /// <summary>
    /// Tries to find specific method using existing list of modules.
    /// </summary>
    /// <param name="methods"></param>
    /// <param name="methodType"></param>
    /// <param name="foundMethod"></param>
    /// <returns></returns>
    public bool TryFindMethod(List<ModuleMethod> methods, Type methodType, [NotNullWhen(returnValue: true)] out ModuleMethod? foundMethod)
    {
        foundMethod = null;

        foreach (var method in methods)
        {
            if (method.GetType() != methodType)
                continue;

            foundMethod = method;
            return true;
        }

        return false;
    }
}
