using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Localizations;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Lathe;

/// <summary>
/// This handles...
/// </summary>
public abstract class SharedLatheSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly EmagSystem _emag = default!;

    public readonly Dictionary<string, List<LatheRecipePrototype>> InverseRecipes = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmagLatheRecipesComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<EmagLatheRecipesComponent, GotUnEmaggedEvent>(OnUnemagged); // Frontier
        SubscribeLocalEvent<LatheComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        BuildInverseRecipeDictionary();
    }

    /// <summary>
    /// Add every recipe in the list of recipe packs to a single hashset.
    /// </summary>
    public void AddRecipesFromPacks(HashSet<ProtoId<LatheRecipePrototype>> recipes, IEnumerable<ProtoId<LatheRecipePackPrototype>> packs)
    {
        foreach (var id in packs)
        {
            var pack = _proto.Index(id);
            recipes.UnionWith(pack.Recipes);
        }
    }

    private void OnExamined(Entity<LatheComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.ReagentOutputSlotId != null)
            args.PushMarkup(Loc.GetString("lathe-menu-reagent-slot-examine"));

        if (ent.Comp.ProductValueModifier != null) // Frontier
            args.PushMarkup(Loc.GetString($"lathe-product-value-modifier", ("modifier", ent.Comp.ProductValueModifier))); // Frontier

    }

    [PublicAPI]
    public bool CanProduce(EntityUid uid, string recipe, int amount = 1, LatheComponent? component = null)
    {
        return _proto.TryIndex<LatheRecipePrototype>(recipe, out var proto) && CanProduce(uid, proto, amount, component);
    }

    // Mono
    public Dictionary<ProtoId<MaterialPrototype>, int> GetEndMaterialAmounts(Entity<LatheComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return new();

        var currentMaterial = _materialStorage.GetStoredMaterials(ent.Owner);
        foreach (var batch in ent.Comp.Queue)
        {
            var recipe = batch.Recipe;
            foreach (var (material, needed) in recipe.Materials)
            {
                var adjustedAmount = AdjustMaterial(needed, recipe.ApplyMaterialDiscount, ent.Comp.FinalMaterialUseMultiplier);
                currentMaterial[material] -= adjustedAmount * (batch.ItemsRequested - batch.ItemsPrinted);
            }
        }
        return currentMaterial;
    }

    // Mono
    /// <summary>
    /// Whether we'll be able to produce this if we queue this to the end.
    /// </summary>
    public bool CanProduceEnd(Entity<LatheComponent?> ent, LatheRecipePrototype recipe, int amount = 1)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;
        if (!HasRecipe(ent, recipe, ent.Comp))
            return false;

        var endAmts = GetEndMaterialAmounts(ent);

        foreach (var (material, needed) in recipe.Materials)
        {
            var adjustedAmount = AdjustMaterial(needed, recipe.ApplyMaterialDiscount, ent.Comp.FinalMaterialUseMultiplier);

            if (endAmts.GetValueOrDefault(material) < adjustedAmount * amount)
                return false;
        }
        return true;
    }

    public bool CanProduce(EntityUid uid, LatheRecipePrototype recipe, int amount = 1, LatheComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;
        if (!HasRecipe(uid, recipe, component))
            return false;

        foreach (var (material, needed) in recipe.Materials)
        {
            var adjustedAmount = AdjustMaterial(needed, recipe.ApplyMaterialDiscount, component.FinalMaterialUseMultiplier);

            if (_materialStorage.GetMaterialAmount(uid, material) < adjustedAmount * amount)
                return false;
        }
        return true;
    }

    private void OnEmagged(EntityUid uid, EmagLatheRecipesComponent component, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(uid, EmagType.Interaction))
            return;

        args.Handled = true;
    }

    // Frontier: demag
    private void OnUnemagged(EntityUid uid, EmagLatheRecipesComponent component, ref GotUnEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (!_emag.CheckFlag(uid, EmagType.Interaction))
            return;

        args.Handled = true;
    }
    // End Frontier: demag

    public static int AdjustMaterial(int original, bool reduce, float multiplier)
        => reduce ? (int) MathF.Ceiling(original * multiplier) : original;

    protected abstract bool HasRecipe(EntityUid uid, LatheRecipePrototype recipe, LatheComponent component);

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs obj)
    {
        if (!obj.WasModified<LatheRecipePrototype>())
            return;
        BuildInverseRecipeDictionary();
    }

    private void BuildInverseRecipeDictionary()
    {
        InverseRecipes.Clear();
        foreach (var latheRecipe in _proto.EnumeratePrototypes<LatheRecipePrototype>())
        {
            if (latheRecipe.Result is not {} result)
                continue;

            InverseRecipes.GetOrNew(result).Add(latheRecipe);
        }
    }

    public bool TryGetRecipesFromEntity(string prototype, [NotNullWhen(true)] out List<LatheRecipePrototype>? recipes)
    {
        recipes = new();
        if (InverseRecipes.TryGetValue(prototype, out var r))
            recipes.AddRange(r);
        return recipes.Count != 0;
    }

    public string GetRecipeName(ProtoId<LatheRecipePrototype> proto)
    {
        return GetRecipeName(_proto.Index(proto));
    }

    public string GetRecipeName(LatheRecipePrototype proto)
    {
        if (!string.IsNullOrWhiteSpace(proto.Name))
            return Loc.GetString(proto.Name);

        if (proto.Result is {} result)
        {
            return _proto.Index(result).Name;
        }

        if (proto.ResultReagents is { } resultReagents)
        {
            return ContentLocalizationManager.FormatList(resultReagents
                .Select(p => Loc.GetString("lathe-menu-result-reagent-display", ("reagent", _proto.Index(p.Key).LocalizedName), ("amount", p.Value)))
                .ToList());
        }

        return string.Empty;
    }

    [PublicAPI]
    public string GetRecipeDescription(ProtoId<LatheRecipePrototype> proto)
    {
        return GetRecipeDescription(_proto.Index(proto));
    }

    public string GetRecipeDescription(LatheRecipePrototype proto)
    {
        if (!string.IsNullOrWhiteSpace(proto.Description))
            return Loc.GetString(proto.Description);

        if (proto.Result is {} result)
        {
            return _proto.Index(result).Description;
        }

        if (proto.ResultReagents is { } resultReagents)
        {
            // We only use the first one for the description since these descriptions don't combine very well.
            var reagent = resultReagents.First().Key;
            return _proto.Index(reagent).LocalizedDescription;
        }

        return string.Empty;
    }

    // Monolith
    /// <summary>
    /// Sets multipliers for this lathe before modification by machine parts, for non-null arguments.
    /// </summary>
    public void SetLatheMultipliers(Entity<LatheComponent?> ent, float? materialUse = null, float? time = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (materialUse != null)
        {
            // parts are server so have to do this hack in shared
            var old = ent.Comp.MaterialUseMultiplier;
            ent.Comp.MaterialUseMultiplier = materialUse.Value;
            ent.Comp.FinalMaterialUseMultiplier *= materialUse.Value / old;

            DirtyField(ent, nameof(LatheComponent.MaterialUseMultiplier));
            DirtyField(ent, nameof(LatheComponent.FinalMaterialUseMultiplier));
        }

        if (time != null)
        {
            var old = ent.Comp.TimeMultiplier;
            ent.Comp.TimeMultiplier = time.Value;
            ent.Comp.FinalTimeMultiplier *= time.Value / old;

            DirtyField(ent, nameof(LatheComponent.TimeMultiplier));
            DirtyField(ent, nameof(LatheComponent.FinalTimeMultiplier));
        }
    }

    // Monolith
    /// <summary>
    /// Multiplies multipliers for this lathe before modification by machine parts, for non-null arguments.
    /// </summary>
    public void MultiplyLatheMultipliers(Entity<LatheComponent?> ent, float? materialUse = null, float? time = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (materialUse != null)
        {
            ent.Comp.MaterialUseMultiplier *= materialUse.Value;
            ent.Comp.FinalMaterialUseMultiplier *= materialUse.Value;

            DirtyField(ent, nameof(LatheComponent.MaterialUseMultiplier));
            DirtyField(ent, nameof(LatheComponent.FinalMaterialUseMultiplier));
        }

        if (time != null)
        {
            ent.Comp.TimeMultiplier *= time.Value;
            ent.Comp.FinalTimeMultiplier *= time.Value;

            DirtyField(ent, nameof(LatheComponent.TimeMultiplier));
            DirtyField(ent, nameof(LatheComponent.FinalTimeMultiplier));
        }
    }
}
