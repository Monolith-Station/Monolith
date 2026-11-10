using System.ComponentModel.DataAnnotations;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Store;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class CurrencyInjection
{
    [DataField(required: true)]
    public string Company;

    [DataField(required: true)]
    public Dictionary<string, FixedPoint2> Amount;
}
