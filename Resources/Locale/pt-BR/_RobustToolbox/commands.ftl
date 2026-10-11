### Localization for engine console commands

cmd-hint-float = [float]

## generic command errors

cmd-parse-failure-mapid = {$arg} não é um MapId válido.
cmd-parse-failure-enum = {$arg} não é um Enum {$enum}.
cmd-parse-failure-grid = {$arg} não é uma grade válida.
cmd-parse-failure-cultureinfo = "{$arg}" não é uma CultureInfo válida.
cmd-parse-failure-session = Não há sessão com o nome de usuário: {$username}

cmd-error-file-not-found = Não foi possível encontrar o arquivo: {$file}.
cmd-error-dir-not-found = Não foi possível encontrar o diretório: {$dir}.

cmd-failure-no-attached-entity = Não há entidade anexada a este console.

## 'cvar_subs' command
cmd-cvar_subs-desc = Lista as assinaturas OnValueChanged de um CVar.
cmd-cvar_subs-help = Uso: {$command} <name>

cmd-cvar_subs-invalid-args = Deve fornecer exatamente um argumento.
cmd-cvar_subs-arg-name = <name>

## 'setambientlight' command
cmd-set-ambient-light-desc = Permite definir a luz ambiente do mapa especificado, em SRGB.
cmd-set-ambient-light-help = Uso: {$command} [mapid] [r g b a]
cmd-set-ambient-light-parse = Não foi possível analisar os argumentos como valores de byte para uma cor.

cmd-ldrsc-desc = Pré-armazena um recurso em cache.
cmd-ldrsc-help = Uso: {$command} <path> <type>

cmd-rldrsc-desc = Recarrega um recurso.
cmd-rldrsc-help = Uso: {$command} <path> <type>

cmd-gridtc-desc = Obtém a contagem de tiles de uma grade.
cmd-gridtc-help = Uso: {$command} <gridId>


# Client-side commands
cmd-guidump-desc = Exporta a árvore da GUI para /guidump.txt nos dados do usuário.
cmd-guidump-help = Uso: {$command}

cmd-uitest-desc = Abre uma janela fictícia para testes de UI.
cmd-uitest-help = Uso: {$command}

## 'uitest2' command
cmd-uitest2-desc = Abre uma janela do sistema operacional para testar controles de UI.
cmd-uitest2-help = Uso: {$command} <tab>
cmd-uitest2-arg-tab = <tab>
cmd-uitest2-error-args = Esperado no máximo um argumento
cmd-uitest2-error-tab = Aba inválida: '{$value}'
cmd-uitest2-title = UITest2


cmd-setclipboard-desc = Define a área de transferência do sistema.
cmd-setclipboard-help = Uso: {$command} <text>

cmd-getclipboard-desc = Obtém a área de transferência do sistema.
cmd-getclipboard-help = Uso: {$command}

cmd-togglelight-desc = Alterna a renderização de luz.
cmd-togglelight-help = Uso: {$command}

cmd-togglefov-desc = Alterna o fov do cliente.
cmd-togglefov-help = Uso: {$command}

cmd-togglehardfov-desc = Alterna o hard fov do cliente. (para depurar space-station-14#2353)
cmd-togglehardfov-help = Uso: {$command}

cmd-toggleshadows-desc = Alterna a renderização de sombras.
cmd-toggleshadows-help = Uso: {$command}

cmd-togglelightbuf-desc = Alterna a renderização de iluminação. Isso inclui sombras, mas não o FOV.
cmd-togglelightbuf-help = Uso: {$command}

cmd-chunkinfo-desc = Obtém informações sobre o chunk sob o cursor do mouse.
cmd-chunkinfo-help = Uso: {$command}

cmd-rldshader-desc = Recarrega todos os shaders.
cmd-rldshader-help = Uso: {$command}

cmd-cldbglyr-desc = Alterna as camadas de depuração de fov e luz.
cmd-cldbglyr-help= Uso: {$command} <layer>: Alterna <layer>
    cldbglyr: Desliga todas as camadas

cmd-key-info-desc = Exibe informações de tecla para uma tecla.
cmd-key-info-help = Uso: {$command} <Key>

## 'bind' command
cmd-bind-desc = Vincula uma combinação de teclas a um comando de entrada.
cmd-bind-help = Uso: {$command} { cmd-bind-arg-key } { cmd-bind-arg-mode } { cmd-bind-arg-command }
    Note que isso NÃO salva as vinculações automaticamente.
    Use o comando 'svbind' para salvar a configuração de vinculações.

cmd-bind-arg-key = <KeyName>
cmd-bind-arg-mode = <BindMode>
cmd-bind-arg-command = <InputCommand>

cmd-net-draw-interp-desc = Alterna o desenho de depuração da interpolação de rede.
cmd-net-draw-interp-help = Uso: {$command}

cmd-net-watch-ent-desc = Exporta todas as atualizações de rede de um EntityId para o console.
cmd-net-watch-ent-help = Uso: {$command} <0|EntityUid>

cmd-net-refresh-desc = Solicita um estado completo do servidor.
cmd-net-refresh-help = Uso: {$command}

cmd-net-entity-report-desc = Alterna o painel de relatório de entidades da rede.
cmd-net-entity-report-help = Uso: {$command}

cmd-fill-desc = Preenche o console para depuração.
cmd-fill-help = Uso: {$command}
                Preenche o console com bobagens para depuração.

cmd-cls-desc = Limpa o console.
cmd-cls-help = Uso: {$command}
               Limpa todas as mensagens do console de depuração.

cmd-sendgarbage-desc = Envia lixo ao servidor.
cmd-sendgarbage-help = Uso: {$command}
                       O servidor responderá com 'no u'

cmd-loadgrid-desc = Carrega uma grade de um arquivo para um mapa existente.
cmd-loadgrid-help = Uso: {$command} <MapID> <Path> [x y] [rotation] [storeUids]

cmd-loc-desc = Imprime no console a localização absoluta da entidade do jogador.
cmd-loc-help = Uso: {$command}

cmd-tpgrid-desc = Teleporta uma grade para uma nova localização.
cmd-tpgrid-help = Uso: {$command} <gridId> <X> <Y> [<MapId>]

cmd-rmgrid-desc = Remove uma grade de um mapa. Você não pode remover a grade padrão.
cmd-rmgrid-help = Uso: {$command} <gridId>

cmd-mapinit-desc = Executa a inicialização de mapa em um mapa.
cmd-mapinit-help = Uso: {$command} <mapID>

cmd-lsmap-desc = Lista mapas.
cmd-lsmap-help = Uso: {$command}

cmd-lsgrid-desc = Lista grades.
cmd-lsgrid-help = Uso: {$command}

cmd-addmap-desc = Adiciona um novo mapa vazio à rodada. Se o mapID já existir, este comando não faz nada.
cmd-addmap-help = Uso: {$command} <mapID> [pre-init]

cmd-rmmap-desc = Remove um mapa do mundo. Você não pode remover o nullspace.
cmd-rmmap-help = Uso: {$command} <mapId>

cmd-savegrid-desc = Serializa uma grade para o disco.
cmd-savegrid-help = Uso: {$command} <gridID> <Path>

cmd-testbed-desc = Carrega um ambiente de testes de física no mapa especificado.
cmd-testbed-help = Uso: {$command} <mapid> <test>

## 'addcomp' command
cmd-addcomp-desc = Adiciona um componente a uma entidade.
cmd-addcomp-help = Uso: {$command} <uid> <componentName>
cmd-addcompc-desc = Adiciona um componente a uma entidade no cliente.
cmd-addcompc-help = Uso: {$command} <uid> <componentName>

## 'rmcomp' command
cmd-rmcomp-desc = Remove um componente de uma entidade.
cmd-rmcomp-help = Uso: {$command} <uid> <componentName>
cmd-rmcompc-desc = Remove um componente de uma entidade no cliente.
cmd-rmcompc-help = Uso: {$command} <uid> <componentName>

## 'addview' command
cmd-addview-desc = Permite assinar a visão de uma entidade para fins de depuração.
cmd-addview-help = Uso: {$command} <entityUid>
cmd-addviewc-desc = Permite assinar a visão de uma entidade para fins de depuração.
cmd-addviewc-help = Uso: {$command} <entityUid>

## 'removeview' command
cmd-removeview-desc = Permite cancelar a assinatura da visão de uma entidade para fins de depuração.
cmd-removeview-help = Uso: {$command} <entityUid>

## 'loglevel' command
cmd-loglevel-desc = Altera o nível de log para um sawmill fornecido.
cmd-loglevel-help = Uso: {$command} <sawmill> <level>
      sawmill: Um rótulo que prefixa mensagens de log. Este é aquele para o qual você está definindo o nível.
      level: O nível de log. Deve corresponder a um dos valores do enum LogLevel.

cmd-testlog-desc = Escreve um log de teste em um sawmill.
cmd-testlog-help = Uso: {$command} <sawmill> <level> <message>
    sawmill: Um rótulo que prefixa a mensagem registrada.
    level: O nível de log. Deve corresponder a um dos valores do enum LogLevel.
    message: A mensagem a ser registrada. Coloque-a entre aspas duplas se quiser usar espaços.

## 'vv' command
cmd-vv-desc = Abre o View Variables.
cmd-vv-help = Uso: {$command} <entity ID|IoC interface name|SIoC interface name>

## 'showvelocities' command
cmd-showvelocities-desc = Exibe suas velocidades angular e linear.
cmd-showvelocities-help = Uso: {$command}

## 'setinputcontext' command
cmd-setinputcontext-desc = Define o contexto de entrada ativo.
cmd-setinputcontext-help = Uso: {$command} <context>

## 'forall' command
cmd-forall-desc = Executa um comando sobre todas as entidades com um determinado componente.
cmd-forall-help = Uso: {$command} <bql query> do <command...>

## 'delete' command
cmd-delete-desc = Exclui a entidade com o ID especificado.
cmd-delete-help = Uso: {$command} <entity UID>

# System commands
cmd-showtime-desc = Mostra a hora do servidor.
cmd-showtime-help = Uso: {$command}

cmd-restart-desc = Reinicia o servidor de forma graciosa (não apenas a rodada).
cmd-restart-help = Uso: {$command}

cmd-shutdown-desc = Desliga o servidor de forma graciosa.
cmd-shutdown-help = Uso: {$command}

cmd-saveconfig-desc = Salva a configuração do servidor no arquivo de configuração.
cmd-saveconfig-help = Uso: {$command}

cmd-netaudit-desc = Exibe informações sobre a segurança de NetMsg.
cmd-netaudit-help = Uso: {$command}

# Player commands
cmd-tp-desc = Teleporta um jogador para qualquer localização da rodada.
cmd-tp-help = Uso: {$command} <x> <y> [<mapID>]

cmd-tpto-desc = Teleporta o jogador atual ou os jogadores/entidades especificados para a localização do primeiro jogador/entidade.
cmd-tpto-help = Uso: {$command} <username|uid> [username|NetEntity]...
cmd-tpto-destination-hint = destino (NetEntity ou nome de usuário)
cmd-tpto-victim-hint = entidade a ser teleportada (NetEntity ou nome de usuário)
cmd-tpto-parse-error = Não foi possível resolver entidade ou jogador: {$str}

cmd-listplayers-desc = Lista todos os jogadores conectados atualmente.
cmd-listplayers-help = Uso: {$command}

cmd-kick-desc = Expulsa um jogador conectado do servidor, desconectando-o.
cmd-kick-help = Uso: {$command} <PlayerIndex> [<Reason>]

# Spin command
cmd-spin-desc = Faz uma entidade girar. A entidade padrão é o pai do jogador anexado.
cmd-spin-help = Uso: {$command} velocity [drag] [entityUid]

# Localization command
cmd-rldloc-desc = Recarrega a localização (cliente e servidor).
cmd-rldloc-help = Uso: {$command}

# Debug entity controls
cmd-spawn-desc = Invoca uma entidade de um tipo específico.
cmd-spawn-help = Uso: {$command} <prototype> | {$command} <prototype> <relative entity ID> | {$command} <prototype> <x> <y>
cmd-cspawn-desc = Invoca uma entidade do lado do cliente de um tipo específico aos seus pés.
cmd-cspawn-help = Uso: {$command} <entity type>

cmd-dumpentities-desc = Exporta a lista de entidades.
cmd-dumpentities-help = Uso: {$command}
                        Exporta a lista de entidades com UIDs e protótipos.

cmd-getcomponentregistration-desc = Obtém informações de registro do componente.
cmd-getcomponentregistration-help = Uso: {$command} <componentName>

cmd-showrays-desc = Alterna o desenho de depuração dos raios de física. Um inteiro para <raylifetime> deve ser fornecido.
cmd-showrays-help = Uso: {$command} <raylifetime>

cmd-disconnect-desc = Desconecta imediatamente do servidor e volta ao menu principal.
cmd-disconnect-help = Uso: {$command}

cmd-entfo-desc = Exibe diagnósticos detalhados de uma entidade.
cmd-entfo-help = Uso: {$command} <entityuid>
    O UID da entidade pode ser prefixado com 'c' para convertê-lo em um UID de entidade do cliente.

cmd-fuck-desc = Lança uma exceção.
cmd-fuck-help = Uso: {$command}

cmd-showpos-desc = Mostra a posição de todas as entidades na tela.
cmd-showpos-help = Uso: {$command}

cmd-showrot-desc = Mostra a rotação de todas as entidades na tela.
cmd-showrot-help = Uso: {$command}

cmd-showvel-desc = Mostra a velocidade local de todas as entidades na tela.
cmd-showvel-help = Uso: {$command}

cmd-showangvel-desc = Mostra a velocidade angular de todas as entidades na tela.
cmd-showangvel-help = Uso: {$command}

cmd-sggcell-desc = Lista entidades em uma célula da snap grid.
cmd-sggcell-help = Uso: {$command} <gridID> <vector2i>\nO parâmetro vector2i está no formato x<int>,y<int>.

cmd-overrideplayername-desc = Altera o nome usado ao tentar conectar ao servidor.
cmd-overrideplayername-help = Uso: {$command} <name>

cmd-showanchored-desc = Mostra as entidades ancoradas em um tile específico.
cmd-showanchored-help = Uso: {$command}

cmd-dmetamem-desc = Exporta os membros de um tipo em um formato adequado para o arquivo de configuração do sandbox.
cmd-dmetamem-help = Uso: {$command} <type>

cmd-launchauth-desc = Carrega tokens de autenticação dos dados do launcher para auxiliar nos testes de servidores ativos.
cmd-launchauth-help = Uso: {$command} <account name>

cmd-lightbb-desc = Alterna a exibição das caixas delimitadoras de luz.
cmd-lightbb-help = Uso: {$command}

cmd-monitorinfo-desc = Mostra informações dos monitores.
cmd-monitorinfo-help = Uso: {$command} <id>

cmd-setmonitor-desc = Define o monitor.
cmd-setmonitor-help = Uso: {$command} <id>

cmd-physics-desc = Mostra uma sobreposição de depuração de física. O argumento fornecido especifica a sobreposição.
cmd-physics-help = Uso: {$command} <aabbs / com / contactnormals / contactpoints / distance / joints / shapeinfo / shapes>

cmd-hardquit-desc = Mata o cliente do jogo instantaneamente.
cmd-hardquit-help = Uso: {$command}
                    Mata o cliente do jogo instantaneamente, sem deixar rastros. Sem se despedir do servidor.

cmd-quit-desc = Encerra o cliente do jogo de forma graciosa.
cmd-quit-help = Uso: {$command}
                Encerra o cliente do jogo corretamente, notificando o servidor conectado e afins.

cmd-csi-desc = Abre um console interativo de C#.
cmd-csi-help = Uso: {$command}

cmd-scsi-desc = Abre um console interativo de C# no servidor.
cmd-scsi-help = Uso: {$command}

cmd-watch-desc = Abre uma janela de observação de variáveis.
cmd-watch-help = Uso: {$command}

cmd-showspritebb-desc = Alterna a exibição dos limites dos sprites.
cmd-showspritebb-help = Uso: {$command}

cmd-togglelookup-desc = Mostra / oculta os limites de entitylookup por meio de uma sobreposição.
cmd-togglelookup-help = Uso: {$command}

cmd-net_entityreport-desc = Alterna o painel de relatório de entidades da rede.
cmd-net_entityreport-help = Uso: {$command}

cmd-net_refresh-desc = Solicita um estado completo do servidor.
cmd-net_refresh-help = Uso: {$command}

cmd-net_graph-desc = Alterna o painel de estatísticas de rede.
cmd-net_graph-help = Uso: {$command}

cmd-net_watchent-desc = Exporta todas as atualizações de rede de um EntityId para o console.
cmd-net_watchent-help = Uso: {$command} <0|EntityUid>

cmd-net_draw_interp-desc = Alterna o desenho de depuração da interpolação de rede.
cmd-net_draw_interp-help = Uso: {$command} <0|EntityUid>

cmd-vram-desc = Exibe estatísticas de uso de memória de vídeo pelo jogo.
cmd-vram-help = Uso: {$command}

cmd-showislands-desc = Mostra os corpos de física atualmente envolvidos em cada ilha de física.
cmd-showislands-help = Uso: {$command}

cmd-showgridnodes-desc = Mostra os nós para fins de divisão de grade.
cmd-showgridnodes-help = Uso: {$command}

cmd-profsnap-desc = Faz um instantâneo de profiling.
cmd-profsnap-help = Uso: {$command}

cmd-devwindow-desc = Janela de desenvolvedor.
cmd-devwindow-help = Uso: {$command}

cmd-scene-desc = Altera imediatamente a cena/estado da UI.
cmd-scene-help = Uso: {$command} <className>

cmd-szr_stats-desc = Relata estatísticas do serializador.
cmd-szr_stats-help = Uso: {$command}

cmd-hwid-desc = Retorna o HWID atual (ID de hardware).
cmd-hwid-help = Uso: {$command}

cmd-vvread-desc = Recupera o valor de um caminho usando o VV (View Variables).
cmd-vvread-help = Uso: {$command} <path>

cmd-vvwrite-desc = Modifica o valor de um caminho usando o VV (View Variables).
cmd-vvwrite-help = Uso: {$command} <path>

cmd-vvinvoke-desc = Invoca/Chama um caminho com argumentos usando o VV.
cmd-vvinvoke-help = Uso: {$command} <path> [arguments...]

cmd-dump_dependency_injectors-desc = Exporta o cache de injetores de dependência do IoCManager.
cmd-dump_dependency_injectors-help = Uso: {$command}
cmd-dump_dependency_injectors-total-count = Contagem total: { $total }

cmd-dump_netserializer_type_map-desc = Exporta o mapa de tipos do NetSerializer e o hash do serializador.
cmd-dump_netserializer_type_map-help = Uso: {$command}

cmd-hub_advertise_now-desc = Anuncia imediatamente ao servidor hub mestre.
cmd-hub_advertise_now-help = Uso: {$command}

cmd-echo-desc = Repete os argumentos de volta ao console.
cmd-echo-help = Uso: {$command} "<message>"

## 'vfs_ls' command
cmd-vfs_ls-desc = Lista o conteúdo de diretórios no VFS.
cmd-vfs_ls-help = Uso: {$command} <path>
    Exemplo:
    vfs_list /Assemblies

cmd-vfs_ls-err-args = É necessário exatamente 1 argumento.
cmd-vfs_ls-hint-path = <path>

cmd-reloadtiletextures-desc = Recarrega o atlas de texturas de tiles para permitir o hot reload de sprites de tiles.
cmd-reloadtiletextures-help = Uso: {$command}

cmd-audio_length-desc = Mostra a duração de um arquivo de áudio
cmd-audio_length-help = Uso: {$command} { cmd-audio_length-arg-file-name }
cmd-audio_length-arg-file-name = <file name>

## PVS
cmd-pvs-override-info-desc = Imprime informações sobre quaisquer overrides de PVS associados a uma entidade.
cmd-pvs-override-info-empty = A entidade {$nuid} não tem overrides de PVS.
cmd-pvs-override-info-global = A entidade {$nuid} tem um override global.
cmd-pvs-override-info-clients = A entidade {$nuid} tem um override de sessão para {$clients}.

cmd-localization_set_culture-desc = Define a DefaultCulture para o LocalizationManager do cliente.
cmd-localization_set_culture-help = Uso: {$command} <cultureName>
cmd-localization_set_culture-culture-name = <cultureName>
cmd-localization_set_culture-changed = Localização alterada para { $code } ({ $nativeName } / { $englishName })

cmd-addmap-hint-2 = runMapInit [true / false]
