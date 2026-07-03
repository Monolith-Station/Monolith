using Content.Shared._Goobstation.Clothing.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Tag;
using Robust.Shared.Serialization.Manager;
using YamlDotNet.Core.Tokens;

namespace Content.Shared._Goobstation.Clothing.Systems;
public sealed partial class ClothingGrantingSystem : EntitySystem
{
    [Dependency] private ISerializationManager _serializationManager = default!;
    [Dependency] private TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingGrantComponentComponent, GotEquippedEvent>(OnCompEquip);
        SubscribeLocalEvent<ClothingGrantComponentComponent, GotUnequippedEvent>(OnCompUnequip);

        SubscribeLocalEvent<ClothingGrantTagComponent, GotEquippedEvent>(OnTagEquip);
        SubscribeLocalEvent<ClothingGrantTagComponent, GotUnequippedEvent>(OnTagUnequip);
    }

    // Monolith Cleanup - Below
    private void OnCompEquip(EntityUid uid, ClothingGrantComponentComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing))
            return;

        if (!clothing.Slots.HasFlag(args.SlotFlags))
            return;

        foreach (var (name, data) in component.Components)
        {
            var newComp = (Component) Factory.GetComponent(name);

            if (HasComp(args.Equipee, newComp.GetType()))
                continue;

            object? temp = newComp;
            _serializationManager.CopyTo(data.Component, ref temp);
            EntityManager.AddComponent(args.Equipee, (Component)temp!);

            component.Active[name] = true; // Goobstation
        }
    }

    private void OnCompUnequip(EntityUid uid, ClothingGrantComponentComponent component, GotUnequippedEvent args)
    {
        foreach (var (name, _) in component.Components)
        {
            if (!component.Active.TryGetValue(name, out _))
                continue;

            var newComp = (Component) Factory.GetComponent(name);

            RemComp(args.Equipee, newComp.GetType());
            component.Active[name] = false;
        }
    }


    private void OnTagEquip(EntityUid uid, ClothingGrantTagComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing))
            return;

        if (!clothing.Slots.HasFlag(args.SlotFlags))
            return;

        EnsureComp<TagComponent>(args.Equipee);
        _tagSystem.AddTag(args.Equipee, component.Tag);

        component.IsActive = true;
    }

    private void OnTagUnequip(EntityUid uid, ClothingGrantTagComponent component, GotUnequippedEvent args)
    {
        if (!component.IsActive)
            return;

        _tagSystem.RemoveTag(args.Equipee, component.Tag);

        component.IsActive = false;
    }
}
