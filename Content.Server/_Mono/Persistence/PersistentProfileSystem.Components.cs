using System.Linq;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Persistence;

public sealed partial class PersistentProfileSystem
{
    private List<PersistentProfileComponent> LoadComponents(
        EntityUid uid,
        IEnumerable<PersistentProfileComponent> components)
    {
        var kept = new List<PersistentProfileComponent>();
        foreach (var component in components)
        {
            if (!TryApplyComponent(uid, component.Data))
            {
                _sawmill.Error("persistence", $"Failed to load a persistent component onto {ToPrettyString(uid)}");
            }
            else if (component.Sticky)
            {
                kept.Add(component);
            }
        }

        return kept;
    }

    public bool TryGetComponents(EntityUid uid, out IReadOnlyList<PersistentProfileComponent> components)
    {
        components = [];
        if (!TryGetProfile(uid, out _, out _, out var profile))
            return false;

        components = profile.Components;
        return true;
    }

    public bool AddComponent(EntityUid uid, string component, bool sticky = false)
    {
        if (!TryGetProfile(uid, out var session, out var slot, out var profile) ||
            profile.Components.Any(entry => entry.Data == component))
        {
            return false;
        }

        var components = new List<PersistentProfileComponent>(profile.Components)
        {
            new(component, sticky),
        };
        SaveProfile(session, slot, profile.WithPersistentData(profile.Flags, components, profile.Items));
        return true;
    }

    public bool AddComponent(EntityUid uid, IComponent component, bool sticky = false)
        => AddComponent(uid, SerializeComponent(component), sticky);

    public bool RemoveComponent(EntityUid uid, string component)
    {
        if (!TryGetProfile(uid, out var session, out var slot, out var profile))
            return false;

        var components = new List<PersistentProfileComponent>(profile.Components);
        var entry = components.FirstOrDefault(value => value.Data == component);
        if (entry == null)
            return false;

        components.Remove(entry);

        SaveProfile(session, slot, profile.WithPersistentData(profile.Flags, components, profile.Items));
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

        return _serialization.WriteValue(
            registry,
            alwaysWrite: true,
            notNullableOverride: true).ToString();
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

        try
        {
            AddComp(uid, component!, overwrite);
            return true;
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to apply a persistent component to {ToPrettyString(uid)}: {e}");
            return false;
        }
    }
}
