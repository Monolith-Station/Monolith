using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Persistence;

public sealed partial class PersistentProfileSystem
{
    public bool TryGetComponents(EntityUid uid, out IReadOnlyList<string> components)
    {
        components = [];
        if (!TryGetProfile(uid, out _, out _, out var profile))
            return false;

        components = profile.Components;
        return true;
    }

    public bool AddComponent(EntityUid uid, string component)
    {
        if (!TryGetProfile(uid, out var session, out var slot, out var profile) ||
            profile.Components.Contains(component))
        {
            return false;
        }

        var components = new List<string>(profile.Components) { component };
        _preferences.SetProfile(session.UserId, slot,
            profile.WithPersistentData(profile.Flags, components, profile.Items)).GetAwaiter().GetResult();
        return true;
    }

    public bool AddComponent(EntityUid uid, IComponent component)
        => AddComponent(uid, SerializeComponent(component));

    public bool RemoveComponent(EntityUid uid, string component)
    {
        if (!TryGetProfile(uid, out var session, out var slot, out var profile))
            return false;

        var components = new List<string>(profile.Components);
        if (!components.Remove(component))
            return false;

        _preferences.SetProfile(session.UserId, slot,
            profile.WithPersistentData(profile.Flags, components, profile.Items)).GetAwaiter().GetResult();
        return true;
    }

    public bool RemoveComponent(EntityUid uid, IComponent component)
        => RemoveComponent(uid, SerializeComponent(component));

    public string SerializeComponent(IComponent component)
    {
        var name = _componentFactory.GetComponentName(component.GetType());
        var registry = new ComponentRegistry
        {
            [name] = new(component, new()),
        };

        return Encode(_serialization.WriteValue(
            registry,
            alwaysWrite: true,
            notNullableOverride: true).ToString());
    }

    public bool TryDeserializeComponent(string data, out IComponent? component)
    {
        component = null;
        try
        {
            var registry = _serialization.Read<ComponentRegistry>(Parse(data), notNullableOverride: true);
            if (registry.Count != 1)
                return false;

            component = registry.Values.Single().Component;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryApplyComponent(EntityUid uid, string data, bool overwrite = true)
    {
        if (!Exists(uid) || !TryDeserializeComponent(data, out var component))
            return false;

        AddComp(uid, component!, overwrite);
        return true;
    }
}
