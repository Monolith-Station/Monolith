### Comandos de consola relacionados con el sistema de votación

## Comando 'createvote'

cmd-createvote-desc = Crea una votación
cmd-createvote-help = Uso: createvote <'restart'|'preset'|'map'>
cmd-createvote-cannot-call-vote-now = ¡No puedes convocar una votación ahora mismo!
cmd-createvote-invalid-vote-type = Tipo de votación no válido
cmd-createvote-arg-vote-type = <tipo de votación>

## Comando 'customvote'

cmd-customvote-desc = Crea una votación personalizada
cmd-customvote-help = Uso: customvote <título> <opción1> <opción2> [opción3...]
cmd-customvote-on-finished-tie = ¡Empate entre {$ties}!
cmd-customvote-on-finished-win = ¡{$winner} gana!
cmd-customvote-arg-title = <título>
cmd-customvote-arg-option-n = <opción{ $n }>

## Comando 'vote'

cmd-vote-desc = Vota en una votación activa
cmd-vote-help = vote <voteId> <opción>
cmd-vote-cannot-call-vote-now = ¡No puedes convocar una votación ahora mismo!
cmd-vote-on-execute-error-must-be-player = Debe ser un jugador
cmd-vote-on-execute-error-invalid-vote-id = ID de votación no válido
cmd-vote-on-execute-error-invalid-vote-options = Opciones de votación no válidas
cmd-vote-on-execute-error-invalid-vote = Votación no válida
cmd-vote-on-execute-error-invalid-option = Opción no válida

## Comando 'listvotes'

cmd-listvotes-desc = Lista las votaciones actualmente activas
cmd-listvotes-help = Uso: listvotes

## Comando 'cancelvote'

cmd-cancelvote-desc = Cancela una votación activa
cmd-cancelvote-help = Uso: cancelvote <id>
                      Puedes obtener el ID con el comando listvotes.
cmd-cancelvote-error-invalid-vote-id = ID de votación no válido
cmd-cancelvote-error-missing-vote-id = ID faltante
cmd-cancelvote-arg-id = <id>
