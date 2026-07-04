using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Projectiles
{
    /// <summary>
    /// Network state for EmbeddedContainerComponent, sending valid embedded entities.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class EmbeddedContainerComponentState : ComponentState
    {
        public HashSet<NetEntity> EmbeddedEntities { get; }

        public EmbeddedContainerComponentState(HashSet<NetEntity> embedded)
        {
            EmbeddedEntities = embedded;
        }
    }

    /// <summary>
    /// System to handle network state for EmbeddedContainerComponent.
    /// </summary>
    public sealed class EmbeddedContainerComponent_NetworkSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<EmbeddedContainerComponent, ComponentGetState>(OnGetState);
            SubscribeLocalEvent<EmbeddedContainerComponent, ComponentHandleState>(OnHandleState);
        }

        private void OnGetState(EntityUid uid, EmbeddedContainerComponent component, ref ComponentGetState args)
        {
            var validSet = new HashSet<NetEntity>();
            foreach (var ent in component.EmbeddedObjects)
            {
                if (TryGetNetEntity(ent, out var netEnt))
                    validSet.Add(netEnt.Value);
            }
            args.State = new EmbeddedContainerComponentState(validSet);
        }

        private void OnHandleState(EntityUid uid, EmbeddedContainerComponent component, ref ComponentHandleState args)
        {
            if (args.Current is not EmbeddedContainerComponentState state)
                return;

            component.EmbeddedObjects.Clear();
            foreach (var netEnt in state.EmbeddedEntities)
            {
                var entityUid = GetEntity(netEnt);
                if (entityUid != EntityUid.Invalid)
                    component.EmbeddedObjects.Add(entityUid);
            }
        }
    }
}
