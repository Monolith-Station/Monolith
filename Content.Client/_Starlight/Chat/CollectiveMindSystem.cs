using Content.Client.Chat.Managers;
using Content.Shared._Starlight.CollectiveMind;
using Robust.Client.Player;

namespace Content.Client.Chat
{
    public sealed partial class CollectiveMindSystem : EntitySystem
    {
        [Dependency] private IChatManager _chatManager = default!;
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private CollectiveMindUpdateSystem _collectiveSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<CollectiveMindComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<CollectiveMindComponent, ComponentRemove>(OnRemove);
        }

        public bool IsCollectiveMind => CompOrNull<CollectiveMindComponent>(_playerManager.LocalPlayer?.ControlledEntity) != null;

        private void OnInit(EntityUid uid, CollectiveMindComponent component, ComponentInit args)
        {
            _chatManager.UpdatePermissions();
        }

        private void OnRemove(EntityUid uid, CollectiveMindComponent component, ComponentRemove args)
        {
            _chatManager.UpdatePermissions();
        }
    }
}
