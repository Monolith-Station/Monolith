### Localización para el comando invoke verb.
# Principalmente mensajes de ayuda y error.

invoke-verb-command-description = Invoca una acción con el nombre dado en una entidad, usando la entidad del jugador
invoke-verb-command-help = invokeverb <playerUid | "self"> <targetUid> <verbName | "interaction" | "activation" | "alternative">

invoke-verb-command-invalid-args = invokeverb requiere 2 argumentos.

invoke-verb-command-invalid-player-uid = El UID del jugador no pudo ser analizado, o no se pasó "self".
invoke-verb-command-invalid-target-uid = El UID del objetivo no pudo ser analizado.

invoke-verb-command-invalid-player-entity = El UID del jugador dado no corresponde a una entidad válida.
invoke-verb-command-invalid-target-entity = El UID del objetivo dado no corresponde a una entidad válida.

invoke-verb-command-success = Se invocó la acción '{ $verb }' en { $target } con { $player } como usuario.

invoke-verb-command-verb-not-found = No se encontró la acción { $verb } en { $target }.
