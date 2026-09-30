using Robust.Client.GameObjects;
using Content.Shared.Lathe;
using Content.Shared.Power;
using Content.Client.Power;
using Content.Client.Storage.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;

namespace Content.Client.Lathe;

public sealed partial class LatheSystem : SharedLatheSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatheComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, LatheComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Lathe specific stuff
        if (_appearance.TryGetData<bool>(uid, LatheVisuals.IsRunning, out var isRunning, args.Component))
        {
            if (args.Sprite.LayerMapTryGet(LatheVisualLayers.IsRunning, out var runningLayer) &&
                component.RunningState != null &&
                component.IdleState != null)
            {
                var state = isRunning ? component.RunningState : component.IdleState;
                args.Sprite.LayerSetState(runningLayer, state);
            }
        }

        if (_appearance.TryGetData<bool>(uid, PowerDeviceVisuals.Powered, out var powered, args.Component) &&
            args.Sprite.LayerMapTryGet(PowerDeviceVisualLayers.Powered, out var powerLayer))
        {
            args.Sprite.LayerSetVisible(powerLayer, powered);

            if (component.UnlitIdleState != null &&
                component.UnlitRunningState != null)
            {
                var state = isRunning ? component.UnlitRunningState : component.UnlitIdleState;
                args.Sprite.LayerSetState(powerLayer, state);
            }
        }
    }

    // Mono
    public override bool CanProduce(EntityUid uid, LatheRecipePrototype recipe, int amount = 1, LatheComponent? component = null)
    {
        if (!TryComp<EntityStorageComponent>(uid, out var storage) &&
            recipe.Entities.Count != 0)
            return false;

        if (storage == null)
            return base.CanProduce(uid, recipe, amount, component);

        foreach (var (entity, needed) in recipe.Entities)
        {
            var processedEntities = 0;
            foreach (var conEnt in storage.Contents.ContainedEntities)
            {
                if (MetaData(conEnt).EntityPrototype?.ID != entity.Id)
                    continue;

                _stackQuery.TryComp(conEnt, out var stack);

                processedEntities += stack?.Count ?? 1;
            }

            if (processedEntities < needed * amount)
                return false;
        }

        return base.CanProduce(uid, recipe, amount, component);
    }

    ///<remarks>
    /// Whether or not a recipe is available is not really visible to the client,
    /// so it just defaults to true.
    ///</remarks>
    protected override bool HasRecipe(EntityUid uid, LatheRecipePrototype recipe, LatheComponent component)
    {
        return true;
    }
}

public enum LatheVisualLayers : byte
{
    IsRunning
}
