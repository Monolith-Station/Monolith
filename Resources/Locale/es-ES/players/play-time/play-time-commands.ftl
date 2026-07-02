parse-minutes-fail = No se puede interpretar '{$minutes}' como minutos
parse-session-fail = No se encontró sesión para '{$username}'

## Comandos del temporizador de rol

# - playtime_addoverall
cmd-playtime_addoverall-desc = Añade los minutos especificados al tiempo de juego total de un jugador
cmd-playtime_addoverall-help = Uso: {$command} <nombre de usuario> <minutos>
cmd-playtime_addoverall-succeed = Tiempo total aumentado para {$username} a {TOSTRING($time, "dddd\\:hh\\:mm")}
cmd-playtime_addoverall-arg-user = <nombre de usuario>
cmd-playtime_addoverall-arg-minutes = <minutos>
cmd-playtime_addoverall-error-args = Se esperaban exactamente dos argumentos

# - playtime_addrole
cmd-playtime_addrole-desc = Añade los minutos especificados al tiempo de juego de un rol de un jugador
cmd-playtime_addrole-help = Uso: {$command} <nombre de usuario> <rol> <minutos>
cmd-playtime_addrole-succeed = Tiempo de juego del rol aumentado para {$username} / \'{$role}\' a {TOSTRING($time, "dddd\\:hh\\:mm")}
cmd-playtime_addrole-arg-user = <nombre de usuario>
cmd-playtime_addrole-arg-role = <rol>
cmd-playtime_addrole-arg-minutes = <minutos>
cmd-playtime_addrole-error-args = Se esperaban exactamente tres argumentos

# - playtime_getoverall
cmd-playtime_getoverall-desc = Obtiene los minutos especificados del tiempo de juego total de un jugador
cmd-playtime_getoverall-help = Uso: {$command} <nombre de usuario>
cmd-playtime_getoverall-success = El tiempo total para {$username} es {TOSTRING($time, "dddd\\:hh\\:mm")}.
cmd-playtime_getoverall-arg-user = <nombre de usuario>
cmd-playtime_getoverall-error-args = Se esperaba exactamente un argumento

# - GetRoleTimer
cmd-playtime_getrole-desc = Obtiene todos o uno de los temporizadores de rol de un jugador
cmd-playtime_getrole-help = Uso: {$command} <nombre de usuario> [rol]
cmd-playtime_getrole-no = No se encontraron temporizadores de rol
cmd-playtime_getrole-role = Rol: {$role}, Tiempo de juego: {$time}
cmd-playtime_getrole-overall = El tiempo de juego total es {$time}
cmd-playtime_getrole-succeed = El tiempo de juego para {$username} es: {TOSTRING($time, "dddd\\:hh\\:mm")}.
cmd-playtime_getrole-arg-user = <nombre de usuario>
cmd-playtime_getrole-arg-role = <rol|'Total'>
cmd-playtime_getrole-error-args = Se esperaba exactamente uno o dos argumentos

# - playtime_save
cmd-playtime_save-desc = Guarda los tiempos de juego del jugador en la base de datos
cmd-playtime_save-help = Uso: {$command} <nombre de usuario>
cmd-playtime_save-succeed = Tiempo de juego guardado para {$username}
cmd-playtime_save-arg-user = <nombre de usuario>
cmd-playtime_save-error-args = Se esperaba exactamente un argumento

## Comando 'playtime_flush'

cmd-playtime_flush-desc = Vuelca los rastreadores activos al almacenamiento en el seguimiento de tiempo de juego.
cmd-playtime_flush-help = Uso: {$command} [nombre de usuario]
    Esto provoca un volcado al almacenamiento interno únicamente, no vuelca a la base de datos de inmediato.
    Si se proporciona un usuario, solo ese usuario se vuelca.

cmd-playtime_flush-error-args = Se esperaba cero o un argumento
cmd-playtime_flush-arg-user = [nombre de usuario]
