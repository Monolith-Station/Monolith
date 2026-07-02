### UI

chat-manager-max-message-length = Tu mensaje supera el límite de {$maxMessageLength} caracteres
chat-manager-ooc-chat-enabled-message = El chat OOC ha sido habilitado.
chat-manager-ooc-chat-disabled-message = El chat OOC ha sido deshabilitado.
chat-manager-looc-chat-enabled-message = El chat LOOC ha sido habilitado.
chat-manager-looc-chat-disabled-message = El chat LOOC ha sido deshabilitado.
chat-manager-dead-looc-chat-enabled-message = Los jugadores muertos ahora pueden usar LOOC.
chat-manager-dead-looc-chat-disabled-message = Los jugadores muertos ya no pueden usar LOOC.
chat-manager-crit-looc-chat-enabled-message = Los jugadores en estado crítico ahora pueden usar LOOC.
chat-manager-crit-looc-chat-disabled-message = Los jugadores en estado crítico ya no pueden usar LOOC.
chat-manager-admin-ooc-chat-enabled-message = El chat OOC de administrador ha sido habilitado.
chat-manager-admin-ooc-chat-disabled-message = El chat OOC de administrador ha sido deshabilitado.

chat-manager-max-message-length-exceeded-message = Tu mensaje superó el límite de {$limit} caracteres
chat-manager-no-headset-on-message = ¡No llevas ningún auricular puesto!
chat-manager-no-radio-key = ¡No se ha especificado ninguna clave de radio!
chat-manager-no-such-channel = ¡No existe ningún canal con la clave '{$key}'!
chat-manager-whisper-headset-on-message = ¡No puedes susurrar por la radio!

chat-manager-server-wrap-message = [bold]{$message}[/bold]
chat-manager-sender-announcement = Mando Central
chat-manager-sender-announcement-wrap-message = [font size=14][bold]{$sender} Anuncio:[/font][font size=12]
                                                {$message}[/bold][/font]
# Einstein Engines - Inicio del lenguaje (cambiando colores para el texto según el color del idioma en el manejador)
# Para el mensaje entre comillas dobles, los elementos de fuente/color/negrita/cursiva se repiten dos veces, fuera de las comillas dobles y dentro.
# Los elementos externos son para formatear las comillas dobles, y los elementos internos son para formatear el texto en los globos de diálogo ([BubbleContent]).
chat-manager-entity-say-wrap-message = [BubbleHeader][bold][Name]{$entityName}[/Name][/bold][/BubbleHeader] {$verb}, [font={$fontType} size={$fontSize}]"[BubbleContent][font="{$fontType}" size={$fontSize}][color={$color}]{$message}[/color][/font][/BubbleContent]"[/font]
chat-manager-entity-say-bold-wrap-message = [BubbleHeader][bold][Name]{$entityName}[/Name][/bold][/BubbleHeader] {$verb}, [font={$fontType} size={$fontSize}]"[BubbleContent][font="{$fontType}" size={$fontSize}][bold][color={$color}]{$message}[/color][/font][/bold][/BubbleContent]"[/font]

chat-manager-entity-whisper-wrap-message = [font size=11][italic][BubbleHeader][Name]{$entityName}[/Name][/BubbleHeader] susurra, "[BubbleContent][color={$color}][font="{$fontType}"]{$message}[/font][/color][/BubbleContent][font size=11]"[/italic][/font]
chat-manager-entity-whisper-unknown-wrap-message = [font size=11][italic][BubbleHeader]Alguien[/BubbleHeader] susurra, "[BubbleContent][color={$color}][font="{$fontType}"]{$message}[/color][/font][/BubbleContent][font size=11]"[/italic][/font]
# Einstein Engines - Fin del lenguaje

# chat-manager-language-prefix = ({ $language }){" "} - Eliminado para que no aparezca; no se desea, pero forma parte del sistema de idiomas.

# THE() no se usa aquí porque la entidad y su nombre pueden estar técnicamente desconectados si se pasa un nameOverride...
chat-manager-entity-me-wrap-message = [italic]{ PROPER($entity) ->
    *[false] El {$entityName} {$message}[/italic]
     [true] {CAPITALIZE($entityName)} {$message}[/italic]
    }

chat-manager-entity-looc-wrap-message = LOOC: [bold]{$entityName}:[/bold] {$message}
chat-manager-send-ooc-wrap-message = OOC: [bold]{$playerName}:[/bold] {$message}
chat-manager-send-ooc-patron-wrap-message = OOC: [bold][color={$patronColor}]{$playerName}[/color]:[/bold] {$message}

chat-manager-send-dead-chat-wrap-message = {$deadChannelName}: [bold][BubbleHeader]{$playerName}[/BubbleHeader]:[/bold] [BubbleContent]{$message}[/BubbleContent]
chat-manager-send-admin-dead-chat-wrap-message = {$title}: [bold]([BubbleHeader]{$userName}[/BubbleHeader]):[/bold] [BubbleContent]{$message}[/BubbleContent]
chat-manager-send-admin-chat-wrap-message = {$adminChannelName}: [bold]{$playerName}:[/bold] {$message}
chat-manager-send-admin-announcement-wrap-message = [bold]{$adminChannelName}: {$message}[/bold]

chat-manager-send-hook-ooc-wrap-message = OOC: [bold](DC) {$senderName}:[/bold] {$message}
chat-manager-send-hook-admin-wrap-message = ADMIN: [bold](DC) {$senderName}:[/bold] {$message}
chat-manager-send-hook-dead-wrap-message = ADMIN: [bold](DC) {$senderName}:[/bold] {$message}

chat-manager-dead-channel-name = MUERTO
chat-manager-admin-channel-name = ADMIN

chat-manager-send-collective-mind-chat-wrap-message = {$channel} mente colectiva: {$message}
chat-manager-send-collective-mind-chat-wrap-message-admin = {$source} ({$channel} mente colectiva): {$message}
chat-manager-collective-mind-channel-name = mente colectiva

chat-manager-rate-limited = ¡Estás enviando mensajes demasiado rápido!
chat-manager-rate-limit-admin-announcement = El jugador { $player } ha superado los límites de velocidad del chat. Vigílalo si esto ocurre con regularidad.

## Verbos de discurso para el chat

chat-speech-verb-suffix-exclamation = !
chat-speech-verb-suffix-exclamation-strong = !!
chat-speech-verb-suffix-question = ?
chat-speech-verb-suffix-stutter = -
chat-speech-verb-suffix-mumble = ..

chat-speech-verb-name-none = Ninguno
chat-speech-verb-name-default = Predeterminado
chat-speech-verb-default = dice
chat-speech-verb-name-exclamation = Exclamando
chat-speech-verb-exclamation = exclama
chat-speech-verb-name-exclamation-strong = Gritando
chat-speech-verb-exclamation-strong = grita
chat-speech-verb-name-question = Preguntando
chat-speech-verb-question = pregunta
chat-speech-verb-name-stutter = Tartamudeando
chat-speech-verb-stutter = tartamudea
chat-speech-verb-name-mumble = Murmurando
chat-speech-verb-mumble = murmura

chat-speech-verb-name-arachnid = Arachnid
chat-speech-verb-insect-1 = traquetea
chat-speech-verb-insect-2 = gorjea
chat-speech-verb-insect-3 = chasquea

chat-speech-verb-name-moth = Moth
chat-speech-verb-winged-1 = aletea
chat-speech-verb-winged-2 = bate
chat-speech-verb-winged-3 = zumba

chat-speech-verb-name-slime = Slime
chat-speech-verb-slime-1 = chapotea
chat-speech-verb-slime-2 = burbujea
chat-speech-verb-slime-3 = rezuma

chat-speech-verb-name-plant = Diona
chat-speech-verb-plant-1 = susurra
chat-speech-verb-plant-2 = se mece
chat-speech-verb-plant-3 = cruje

chat-speech-verb-name-robotic = Robótico
chat-speech-verb-robotic-1 = declara
chat-speech-verb-robotic-2 = pita
chat-speech-verb-robotic-3 = toca

chat-speech-verb-name-reptilian = Reptiliano
chat-speech-verb-reptilian-1 = sisea
chat-speech-verb-reptilian-2 = resopla
chat-speech-verb-reptilian-3 = bufa

chat-speech-verb-name-skeleton = Esqueleto
chat-speech-verb-skeleton-1 = traquetea
chat-speech-verb-skeleton-2 = castañetea
chat-speech-verb-skeleton-3 = rechina

chat-speech-verb-name-vox = Vox
chat-speech-verb-vox-1 = chilla
chat-speech-verb-vox-2 = grita
chat-speech-verb-vox-3 = croa

chat-speech-verb-name-canine = Canino
chat-speech-verb-canine-1 = ladra
chat-speech-verb-canine-2 = ladra
chat-speech-verb-canine-3 = aúlla

chat-speech-verb-name-goat = Cabra
chat-speech-verb-goat-1 = bala
chat-speech-verb-goat-2 = gruñe
chat-speech-verb-goat-3 = berrea

chat-speech-verb-name-small-mob = Ratón
chat-speech-verb-small-mob-1 = chilla
chat-speech-verb-small-mob-2 = pía

chat-speech-verb-name-large-mob = Carpa
chat-speech-verb-large-mob-1 = ruge
chat-speech-verb-large-mob-2 = gruñe

chat-speech-verb-name-monkey = Mono
chat-speech-verb-monkey-1 = parlotea
chat-speech-verb-monkey-2 = chilla

chat-speech-verb-name-cluwne = Cluwne

chat-speech-verb-name-parrot = Loro
chat-speech-verb-parrot-1 = grazna
chat-speech-verb-parrot-2 = gorjea
chat-speech-verb-parrot-3 = pía

chat-speech-verb-cluwne-1 = se ríe
chat-speech-verb-cluwne-2 = suelta carcajadas
chat-speech-verb-cluwne-3 = ríe

chat-speech-verb-name-ghost = Fantasma
chat-speech-verb-ghost-1 = se queja
chat-speech-verb-ghost-2 = respira
chat-speech-verb-ghost-3 = tararea
chat-speech-verb-ghost-4 = masculla

chat-speech-verb-name-electricity = Electricidad
chat-speech-verb-electricity-1 = crepita
chat-speech-verb-electricity-2 = zumba
chat-speech-verb-electricity-3 = chilla
