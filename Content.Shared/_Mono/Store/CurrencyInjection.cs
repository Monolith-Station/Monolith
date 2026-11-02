using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Store;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class CurrencyInjection
{
    [DataField]
    public string Company;

    [DataField]
    public Dictionary<string, FixedPoint2> Amount;
}
