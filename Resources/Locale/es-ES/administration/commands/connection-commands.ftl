## Cadenas para el comando "grant_connect_bypass".

cmd-grant_connect_bypass-desc = Permite temporalmente que un usuario omita las comprobaciones de conexión habituales.
cmd-grant_connect_bypass-help = Uso: grant_connect_bypass <usuario> [duración en minutos]
    Otorga temporalmente a un usuario la capacidad de omitir las restricciones de conexión habituales.
    El bypass solo se aplica a este servidor de juego y expirará después de (por defecto) 1 hora.
    Podrán unirse independientemente de la lista blanca, el bunker de pánico o el límite de jugadores.

cmd-grant_connect_bypass-arg-user = <usuario>
cmd-grant_connect_bypass-arg-duration = [duración en minutos]

cmd-grant_connect_bypass-invalid-args = Se esperaba 1 o 2 argumentos
cmd-grant_connect_bypass-unknown-user = No se pudo encontrar al usuario '{$user}'
cmd-grant_connect_bypass-invalid-duration = Duración inválida '{$duration}'

cmd-grant_connect_bypass-success = Bypass añadido correctamente para el usuario '{$user}'
