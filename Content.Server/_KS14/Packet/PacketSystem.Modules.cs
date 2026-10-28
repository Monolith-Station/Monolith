using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server._KS14.Packet.Modules;
using Content.Server._KS14.Packet.States;
using Content.Shared._KS14.Packets;
using Content.Shared._KS14.Packets.Widgets;
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
    private List<Type> _callTypes = [];
    private List<Type> _widgetTypes = [];
    private List<DeviceCallEntry> _classEntries = [];

    /// <summary>
    /// Loads <see cref="PacketModule"/> and <see cref="ModuleMethod"/> types into lists upon system initialization.
    /// </summary>
    private void PreInitJint()
    {
        LoadType(ref _methodTypes, typeof(ModuleMethod));
        LoadType(ref _moduleTypes, typeof(PacketModule));
        LoadType(ref _callTypes, typeof(DeviceCall));
        LoadType(ref _widgetTypes, typeof(WidgetElement));

        _classEntries = [.. GeneratePacketClassEntries(_callTypes)];
    }

    private void LoadType(ref List<Type> types, Type loadType)
    {
        types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => loadType.IsAssignableFrom(type)
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

    public List<DeviceCallEntry> GeneratePacketClassEntries(List<Type> types)
    {
        List<DeviceCallEntry> entries = [];

        foreach (var type in types)
        {
            entries.Add(GeneratePacketClassEntry(type));
        }

        return entries;
    }

    public DeviceCallEntry GeneratePacketClassEntry(Type type)
    {
        var name = type.Name;
        var inFields = new Dictionary<string, string>();
        var outFields = new Dictionary<string, string>();

        FormatFields(type.GetProperties(),  ref inFields, ref outFields);
        FormatFields(type.GetFields(),  ref inFields, ref outFields);

        return new DeviceCallEntry(name, inFields, outFields);
    }

    private void FormatFields(PropertyInfo[] info, ref Dictionary<string, string> inFields, ref Dictionary<string, string> outFields)
    {
        foreach (var field in info)
        {
            if (field.GetCustomAttribute(typeof(PacketFieldAttribute)) is not PacketFieldAttribute attribute)
                continue;

            switch (attribute.FieldType)
            {
                case PacketFieldType.Accepts:
                    inFields.Add(field.Name, field.PropertyType.Name);
                    break;
                case PacketFieldType.Returns:
                    outFields.Add(field.Name, field.PropertyType.Name);
                    break;
            }
        }
    }

    private void FormatFields(FieldInfo[] info, ref Dictionary<string, string> inFields, ref Dictionary<string, string> outFields)
    {
        foreach (var field in info)
        {
            if (field.GetCustomAttribute(typeof(PacketFieldAttribute)) is not PacketFieldAttribute attribute)
                continue;

            switch (attribute.FieldType)
            {
                case PacketFieldType.Accepts:
                    inFields.Add(field.Name, field.FieldType.Name);
                    break;
                case PacketFieldType.Returns:
                    outFields.Add(field.Name, field.FieldType.Name);
                    break;
            }
        }
    }
}
