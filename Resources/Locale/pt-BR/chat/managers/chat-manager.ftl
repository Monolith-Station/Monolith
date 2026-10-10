### UI

chat-manager-max-message-length = Sua mensagem excede o limite de {$maxMessageLength} caracteres
chat-manager-ooc-chat-enabled-message = Chat OOC foi habilitado.
chat-manager-ooc-chat-disabled-message = Chat OOC foi desabilitado.
chat-manager-looc-chat-enabled-message = Chat LOOC foi habilitado.
chat-manager-looc-chat-disabled-message = Chat LOOC foi desabilitado.
chat-manager-dead-looc-chat-enabled-message = Jogadores mortos agora podem usar LOOC.
chat-manager-dead-looc-chat-disabled-message =morto players pode não longer usar LOOC.
chat-manager-crit-looc-chat-enabled-message = Jogadores em estado crítico agora podem usar LOOC.
chat-manager-crit-looc-chat-disabled-message =Crit players pode não longer usar LOOC.
chat-manager-admin-ooc-chat-enabled-message = Chat OOC de Admin foi habilitado.
chat-manager-admin-ooc-chat-disabled-message = Chat OOC de Admin foi desabilitado.

chat-manager-max-message-length-exceeded-message = Sua mensagem excedeu o limite de {$limit} caracteres
chat-manager-no-headset-on-message = Você não está usando um fone de ouvido!
chat-manager-no-radio-key = Nenhuma chave de rádio especificada!
chat-manager-no-such-channel = Não existe canal com chave '{$key}'!
chat-manager-whisper-headset-on-message = Você não consegue sussurrar no rádio!

chat-manager-server-wrap-message = [bold]{$message}[/bold]
chat-manager-sender-announcement = Comando Central
chat-manager-sender-announcement-wrap-message = [font size=14][bold]{$sender} anuncia:[/font][font size=12]
                                                {$message}[/bold][/font]
# Einstein Engines - Language begin (changing colors for text based on language color in handler)
# For the message in double quotes, the font/color/bold/italic elements are repeated twice, outside the double quotes and inside.
# The outside elements are for formatting the double quotes, and the inside elements are for formatting the text in speech bubbles ([BubbleContent]).
chat-manager-entity-say-wrap-message =[BubbleHeader][bold][nome]{$entityName}[/nome][/bold][/BubbleHeader] {$verb}, [font={$fontType} tamanho={$fontSize}]"[BubbleContent][font="{$fontType}" tamanho={$fontSize}][color={$color}]{$message}[/color][/font][/BubbleContent]"[/font]
chat-manager-entity-say-bold-wrap-message =[BubbleHeader][bold][nome]{$entityName}[/nome][/bold][/BubbleHeader] {$verb}, [font={$fontType} tamanho={$fontSize}]"[BubbleContent][font="{$fontType}" tamanho={$fontSize}][bold][color={$color}]{$message}[/color][/font][/bold][/BubbleContent]"[/font]

chat-manager-entity-whisper-wrap-message = [font size=11][italic][BubbleHeader][Name]{$entityName}[/Name][/BubbleHeader] sussurra, "[BubbleContent][color={$color}][font="{$fontType}"]{$message}[/font][/color][/BubbleContent][font size=11]"[/italic][/font]
chat-manager-entity-whisper-unknown-wrap-message = [font size=11][italic][BubbleHeader]Alguém[/BubbleHeader] sussurra, "[BubbleContent][color={$color}][font="{$fontType}"]{$message}[/color][/font][/BubbleContent][font size=11]"[/italic][/font]
# Einstein Engines - Language end

# chat-manager-language-prefix = ({ $language }){" "} - Removed so it doesn't show up, not wanted, but part of the language system.

# THE() is not used here because the entity and its name can technically be disconnected if a nameOverride is passed...
chat-manager-entity-me-wrap-message = [italic]{ PROPER($entity) ->
    *[false] O {$entityName} {$message}[/italic]
     [true] {CAPITALIZE($entityName)} {$message}[/italic]
    }

chat-manager-entity-looc-wrap-message =LOOC: [bold]{$entityName}:[/bold] {$message}
chat-manager-send-ooc-wrap-message =OOC: [bold]{$playerName}:[/bold] {$message}
chat-manager-send-ooc-patron-wrap-message =OOC: [bold][color={$patronColor}]{$playerName}[/color]:[/bold] {$message}

chat-manager-send-dead-chat-wrap-message = {$deadChannelName}: [bold][BubbleHeader]{$playerName}[/BubbleHeader]:[/bold] [BubbleContent]{$message}[/BubbleContent]
chat-manager-send-admin-dead-chat-wrap-message = {$title}: [bold]([BubbleHeader]{$userName}[/BubbleHeader]):[/bold] [BubbleContent]{$message}[/BubbleContent]
chat-manager-send-admin-chat-wrap-message = {$adminChannelName}: [bold]{$playerName}:[/bold] {$message}
chat-manager-send-admin-announcement-wrap-message = [bold]{$adminChannelName}: {$message}[/bold]

chat-manager-send-hook-ooc-wrap-message =OOC: [bold](DC) {$senderName}:[/bold] {$message}
chat-manager-send-hook-admin-wrap-message =ADMIN: [bold](DC) {$senderName}:[/bold] {$message}
chat-manager-send-hook-dead-wrap-message =ADMIN: [bold](DC) {$senderName}:[/bold] {$message}

chat-manager-dead-channel-name = MORTO
chat-manager-admin-channel-name =ADMIN

chat-manager-send-collective-mind-chat-wrap-message = mente coletiva {$channel}: {$message}
chat-manager-send-collective-mind-chat-wrap-message-admin = {$source} (mente coletiva {$channel}): {$message}
chat-manager-collective-mind-channel-name = mente coletiva

# eu que fiz a tradução, eu posso SIM botar um easter egg.
# valor antigo: Você está enviando mensagens muito rápido!
chat-manager-rate-limited = CHEGA DE SPAM CARALHO
chat-manager-rate-limit-admin-announcement = Jogador { $player } excedeu os limites de taxa de chat. Monitore se for uma ocorrência regular.

## Speech verbs for chat

chat-speech-verb-suffix-exclamation =!
chat-speech-verb-suffix-exclamation-strong =!!
chat-speech-verb-suffix-question =?
chat-speech-verb-suffix-stutter =-
chat-speech-verb-suffix-mumble =..

chat-speech-verb-name-none = Nenhum
chat-speech-verb-name-default = Padrão
chat-speech-verb-default = diz
chat-speech-verb-name-exclamation = Exclamando
chat-speech-verb-exclamation = exclama
chat-speech-verb-name-exclamation-strong = Gritando
chat-speech-verb-exclamation-strong = grita
chat-speech-verb-name-question = Perguntando
chat-speech-verb-question = pergunta
chat-speech-verb-name-stutter = Gaguejando
chat-speech-verb-stutter = gagueja
chat-speech-verb-name-mumble = Murmurando
chat-speech-verb-mumble = murmura

chat-speech-verb-name-arachnid = Aracnídeo
chat-speech-verb-insect-1 = chilreia
chat-speech-verb-insect-2 = chirria
chat-speech-verb-insect-3 = clica

chat-speech-verb-name-moth = Traça
chat-speech-verb-winged-1 = bate asas
chat-speech-verb-winged-2 = flapaneia
chat-speech-verb-winged-3 = zurruma

chat-speech-verb-name-slime =Slime
chat-speech-verb-slime-1 = esborracha
chat-speech-verb-slime-2 = borbulha
chat-speech-verb-slime-3 = exuda

chat-speech-verb-name-plant =Diona
chat-speech-verb-plant-1 = rumoreja
chat-speech-verb-plant-2 = balança
chat-speech-verb-plant-3 = range

chat-speech-verb-name-robotic = Robótico
chat-speech-verb-robotic-1 = declara
chat-speech-verb-robotic-2 = bipa
chat-speech-verb-robotic-3 = boopa

chat-speech-verb-name-reptilian = Reptiliano
chat-speech-verb-reptilian-1 = sibila
chat-speech-verb-reptilian-2 = resfolega
chat-speech-verb-reptilian-3 = sopra

chat-speech-verb-name-skeleton = Esqueleto
chat-speech-verb-skeleton-1 = chacoalha
chat-speech-verb-skeleton-2 = claqueteia
chat-speech-verb-skeleton-3 = range os dentes

chat-speech-verb-name-vox =Vox
chat-speech-verb-vox-1 = estridencia
chat-speech-verb-vox-2 = grita
chat-speech-verb-vox-3 = coaxa

chat-speech-verb-name-canine = Canino
chat-speech-verb-canine-1 = late
chat-speech-verb-canine-2 = auau
chat-speech-verb-canine-3 = uiva

chat-speech-verb-name-goat = Bode
chat-speech-verb-goat-1 = bale
chat-speech-verb-goat-2 = grunhe
chat-speech-verb-goat-3 = chora

chat-speech-verb-name-small-mob = Camundongo
chat-speech-verb-small-mob-1 = guincha
chat-speech-verb-small-mob-2 = pía

chat-speech-verb-name-large-mob = Carpa
chat-speech-verb-large-mob-1 = ruge
chat-speech-verb-large-mob-2 = rosna

chat-speech-verb-name-monkey = Macaco
chat-speech-verb-monkey-1 = chimpa
chat-speech-verb-monkey-2 = estridula

chat-speech-verb-name-cluwne =Cluwne

chat-speech-verb-name-parrot = Papagaio
chat-speech-verb-parrot-1 = grita
chat-speech-verb-parrot-2 = trinca
chat-speech-verb-parrot-3 = chirria

chat-speech-verb-cluwne-1 = gargalha
chat-speech-verb-cluwne-2 = gargalha alto
chat-speech-verb-cluwne-3 = ri

chat-speech-verb-name-ghost = Fantasma
chat-speech-verb-ghost-1 = reclama
chat-speech-verb-ghost-2 = suspira
chat-speech-verb-ghost-3 = murmura
chat-speech-verb-ghost-4 = resmunga

chat-speech-verb-name-electricity = Eletricidade
chat-speech-verb-electricity-1 = crepita
chat-speech-verb-electricity-2 = zumba
chat-speech-verb-electricity-3 = estridencia
