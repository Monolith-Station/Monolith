using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.CollectiveMind
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class CollectiveMindComponent : Component
    {
        [DataField("minds")]
        public Dictionary<string, int> Minds = new();

        [DataField, AutoNetworkedField]
        public ProtoId<CollectiveMindPrototype>? DefaultChannel = null;

        [DataField, AutoNetworkedField]
        public HashSet<ProtoId<CollectiveMindPrototype>> Channels = new();

        [DataField]
        public bool HearAll = false;

        [DataField]
        public bool SeeAllNames = false;
    }
}
