using Content.Shared.Damage;
using Content.Shared.Whitelist;

namespace Content.Shared._Mono.Fixtures;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class FixtureDamageDealerComponent : Component
{
    [DataField]
    public int CollisionMask = 0;

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField]
    public EntityWhitelist? Whitelist;
}
