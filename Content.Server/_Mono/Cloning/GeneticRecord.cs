using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Cloning;

/// <summary>
/// snapshot
/// </summary>
public sealed class GeneticRecord
{
    public int Id;

    public EntityUid Mind;

    public string Name = string.Empty;

    public ProtoId<SpeciesPrototype> Species;

    public HumanoidAppearanceComponent Appearance = default!;

    public float Mass;

    public float FailChance;

    public bool HasBankAccount;

    public List<IComponent> TransferredComponents = new();

    public TimeSpan ScannedAt;

    public bool Uncloneable;
}
