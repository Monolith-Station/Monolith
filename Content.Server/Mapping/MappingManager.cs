using System.IO;
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Mapping;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using Content.Shared._Mono.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server.Mapping;

public sealed partial class MappingManager : IPostInjectInit
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IMapManager _map = default!;
    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IEntitySystemManager _systems = default!;
    [Dependency] private IEntityManager _ent = default!;
    [Dependency] private ISerializationManager _serialization = default!; //Reserve - Wizden mapping editor
    [Dependency] private IResourceManager _resourceMan = default!; //Reserve - Wizden mapping editor
    [Dependency] private IConfigurationManager _config = default!; //Reserve - Wizden mapping editor

    private ISawmill _sawmill = default!;
    private ZStdCompressionContext _zstd = default!;

    private const string FavoritesPath = "/mapping_editor_favorites.yml"; //Reserve - Wizden mapping editor

    //Reserve - Wizden mapping editor begin
    public void PostInject()
    {
        _net.RegisterNetMessage<MappingFavoritesSaveMessage>(OnMappingFavoritesSave);
        _net.RegisterNetMessage<MappingFavoritesLoadMessage>(OnMappingFavoritesLoad);
        _net.RegisterNetMessage<MappingFavoritesDataMessage>();

        _sawmill = _log.GetSawmill("mapping");
        //Reserve - Wizden mapping editor end

#if !FULL_RELEASE
        _net.RegisterNetMessage<MappingSaveMapMessage>(OnMappingSaveMap);
        _net.RegisterNetMessage<MappingSaveMapErrorMessage>();
        _net.RegisterNetMessage<MappingMapDataMessage>();

        _zstd = new ZStdCompressionContext();
#endif
    }

    private void OnMappingSaveMap(MappingSaveMapMessage message)
    {
#if !FULL_RELEASE
        try
        {
            if (!_players.TryGetSessionByChannel(message.MsgChannel, out var session) ||
                !_admin.IsAdmin(session, true) ||
                !_admin.HasAdminFlag(session, AdminFlags.Host) ||
                !_ent.TryGetComponent(session.AttachedEntity, out TransformComponent? xform) ||
                xform.MapUid is not { } mapUid)
            {
                return;
            }

            var sys = _systems.GetEntitySystem<MapLoaderSystem>();
            var data = sys.SerializeEntitiesRecursive([mapUid]).Node;
            var document = new YamlDocument(data.ToYaml());
            var stream = new YamlStream { document };
            var writer = new StringWriter();
            stream.Save(new YamlMappingFix(new Emitter(writer)), false);

            var msg = new MappingMapDataMessage()
            {
                Context = _zstd,
                Yml = writer.ToString()
            };
            _net.ServerSendMessage(msg, message.MsgChannel);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error saving map in mapping mode:\n{e}");
            var msg = new MappingSaveMapErrorMessage();
            _net.ServerSendMessage(msg, message.MsgChannel);
        }
#endif
    }

    //Reserve - Wizden mapping editor begin
    private void OnMappingFavoritesSave(MappingFavoritesSaveMessage message)
    {
        if (!_config.GetCVar(MonoCVars.MappingEditorFavoritesSaving))
        {
            _sawmill.Error("Saving of favorites not enabled. To continue, toggle cvar mono.mapping_editor_favorites_saving .");
            return;
        }

        var mapping = new MappingDataNode
        {
            { "prototypes", _serialization.WriteValue(message.PrototypeIDs, notNullableOverride: true) }
        };

        var path = new ResPath(FavoritesPath);
        using var writer = _resourceMan.UserData.OpenWriteText(path);
        var stream = new YamlStream { new(mapping.ToYaml()) };
        stream.Save(new YamlMappingFix(new Emitter(writer)), false);
    }

    private void OnMappingFavoritesLoad(MappingFavoritesLoadMessage message)
    {
        if (!_config.GetCVar(MonoCVars.MappingEditorFavoritesSaving))
        {
            _sawmill.Error("Saving of favorites not enabled. To continue, toggle cvar mono.mapping_editor_favorites_saving .");
            return;
        }

        var path = new ResPath(FavoritesPath);

        if (!_resourceMan.UserData.Exists(path))
            return;

        try
        {
            var reader = _resourceMan.UserData.OpenText(path);
            var documents = DataNodeParser.ParseYamlStream(reader).First();
            var mapping = (MappingDataNode)documents.Root;

            if (!mapping.TryGet("prototypes", out var prototypesNode))
                return;

            var ids = _serialization.Read<string[]>(prototypesNode, notNullableOverride: true).ToList();

            var msg = new MappingFavoritesDataMessage()
            {
                PrototypeIDs = ids,
            };
            _net.ServerSendMessage(msg, message.MsgChannel);
        }
        catch (Exception e)
        {
            _sawmill.Error("Failed to load user favorite objects: " + e);
        }
    }
    //Reserve - Wizden mapping editor end
}
