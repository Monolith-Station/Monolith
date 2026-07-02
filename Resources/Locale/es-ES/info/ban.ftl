# ban
cmd-ban-desc = Banea a alguien
cmd-ban-help = Uso: ban <nombre o ID de usuario> <razón> [duración en minutos, omitir o 0 para baneo permanente]
cmd-ban-player = No se puede encontrar un jugador con ese nombre.
cmd-ban-invalid-minutes = ¡{$minutes} no es una cantidad de minutos válida!
cmd-ban-invalid-severity = ¡{$severity} no es una gravedad válida!
cmd-ban-invalid-arguments = Cantidad de argumentos inválida
cmd-ban-hint = <nombre/ID de usuario>
cmd-ban-hint-reason = <razón>
cmd-ban-hint-duration = [duración]
cmd-ban-hint-severity = [gravedad]

cmd-ban-hint-duration-1 = Permanente
cmd-ban-hint-duration-2 = 1 día
cmd-ban-hint-duration-3 = 3 días
cmd-ban-hint-duration-4 = 1 semana
cmd-ban-hint-duration-5 = 2 semanas
cmd-ban-hint-duration-6 = 1 mes

# panel de baneo
cmd-banpanel-desc = Abre el panel de baneo
cmd-banpanel-help = Uso: banpanel [nombre o GUID de usuario]
cmd-banpanel-server = Esto no puede usarse desde la consola del servidor
cmd-banpanel-player-err = El jugador especificado no pudo ser encontrado

# listbans
cmd-banlist-desc = Lista los baneos activos de un usuario.
cmd-banlist-help = Uso: banlist <nombre o ID de usuario>
cmd-banlist-empty = No se encontraron baneos activos para {$user}
cmd-banlistF-hint = <nombre/ID de usuario>

cmd-ban_exemption_update-desc = Establece una exención a un tipo de baneo para un jugador.
cmd-ban_exemption_update-help = Uso: ban_exemption_update <jugador> <bandera> [<bandera> [...]]
    Especifica múltiples banderas para dar a un jugador múltiples banderas de exención de baneo.
    Para eliminar todas las exenciones, ejecuta este comando y da "None" como única bandera.

cmd-ban_exemption_update-nargs = Se esperaban al menos 2 argumentos
cmd-ban_exemption_update-locate = No se puede localizar al jugador '{$player}'.
cmd-ban_exemption_update-invalid-flag = Bandera inválida '{$flag}'.
cmd-ban_exemption_update-success = Se actualizaron las banderas de exención de baneo para '{$player}' ({$uid}).
cmd-ban_exemption_update-arg-player = <jugador>
cmd-ban_exemption_update-arg-flag = <bandera>

cmd-ban_exemption_get-desc = Muestra las exenciones de baneo para un jugador determinado.
cmd-ban_exemption_get-help = Uso: ban_exemption_get <jugador>

cmd-ban_exemption_get-nargs = Se esperaba exactamente 1 argumento
cmd-ban_exemption_get-none = El usuario no está exento de ningún baneo.
cmd-ban_exemption_get-show = El usuario está exento de las siguientes banderas de baneo: {$flags}.
cmd-ban_exemption_get-arg-player = <jugador>

# Panel de baneo
ban-panel-title = Panel de baneo
ban-panel-player = Jugador
ban-panel-ip = IP
ban-panel-hwid = HWID
ban-panel-reason = Razón
ban-panel-last-conn = ¿Usar la IP y HWID de la última conexión?
ban-panel-submit = Banear
ban-panel-confirm = ¿Estás seguro/a?
ban-panel-tabs-basic = Información básica
ban-panel-tabs-reason = Razón
ban-panel-tabs-players = Lista de jugadores
ban-panel-tabs-role = Información de baneo por rol
ban-panel-no-data = Debes proporcionar un usuario, IP o HWID para banear
ban-panel-invalid-ip = La dirección IP no pudo ser procesada. Por favor, inténtalo de nuevo
ban-panel-select = Seleccionar tipo
ban-panel-server = Baneo del servidor
ban-panel-role = Baneo de rol
ban-panel-minutes = Minutos
ban-panel-hours = Horas
ban-panel-days = Días
ban-panel-weeks = Semanas
ban-panel-months = Meses
ban-panel-years = Años
ban-panel-permanent = Permanente
ban-panel-ip-hwid-tooltip = Deja vacío y marca la casilla de abajo para usar los datos de la última conexión
ban-panel-severity = Gravedad:
ban-panel-erase = Borrar mensajes del chat y al jugador de la partida

# Cadena de baneo
server-ban-string = {$admin} creó un baneo del servidor de gravedad {$severity} que expira {$expires} para [{$name}, {$ip}, {$hwid}], con razón: {$reason}
server-ban-string-no-pii = {$admin} creó un baneo del servidor de gravedad {$severity} que expira {$expires} para {$name} con razón: {$reason}
server-ban-string-never = nunca

# Expulsión por baneo
ban-kick-reason = Has sido baneado/a
