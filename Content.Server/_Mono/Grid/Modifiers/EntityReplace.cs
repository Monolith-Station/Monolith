using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Grid.Modifiers;

[UsedImplicitly]
public sealed partial class EntityReplace : GridModifier
{
    [DataField(required: true)]
    public List<ReplaceData> Data = [];

    public override void Modify(EntityUid uid, MetaDataComponent meta, TransformComponent xform, EntityManager system)
    {
        var whitelistSystem = system.System<EntityWhitelistSystem>();

       if (meta.EntityPrototype == null)
           return;

       foreach (var rD in Data)
       {
           if (whitelistSystem.IsWhitelistFailOrNull(rD.Whitelist, uid) && meta.EntityPrototype.ID != rD.ToReplace)
               continue;

           var random = new Random();
           if (random.NextSingle() > rD.Chance)
               continue;

           var pos = xform.Coordinates;

           system.QueueDeleteEntity(uid);
           system.SpawnAtPosition(rD.ReplaceWith, pos);
       }
    }

}

[DataDefinition]
[Serializable]
public sealed partial class ReplaceData
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntProtoId? ToReplace;

    [DataField(required: true)]
    public EntProtoId ReplaceWith;

    [DataField]
    public float Chance = 0.2f;
}
