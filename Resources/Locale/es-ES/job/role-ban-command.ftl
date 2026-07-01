### Traducción para el comando de prohibición de rol

cmd-roleban-desc = Prohíbe a un jugador de un rol
cmd-roleban-help = Uso: roleban <nombre o ID de usuario> <trabajo> <razón> [duración en minutos, omitir o 0 para prohibición permanente]

## Sugerencias de resultados de completado
cmd-roleban-hint-1 = <nombre o ID de usuario>
cmd-roleban-hint-2 = <trabajo>
cmd-roleban-hint-3 = <razón>
cmd-roleban-hint-4 = [duración en minutos, omitir o 0 para prohibición permanente]
cmd-roleban-hint-5 = [gravedad]

cmd-roleban-hint-duration-1 = Permanente
cmd-roleban-hint-duration-2 = 1 día
cmd-roleban-hint-duration-3 = 3 días
cmd-roleban-hint-duration-4 = 1 semana
cmd-roleban-hint-duration-5 = 2 semanas
cmd-roleban-hint-duration-6 = 1 mes


### Traducción para el comando de desprohibición de rol

cmd-roleunban-desc = Perdona la prohibición de rol de un jugador
cmd-roleunban-help = Uso: roleunban <id de prohibición de rol>

## Sugerencias de resultados de completado
cmd-roleunban-hint-1 = <id de prohibición de rol>


### Traducción para el comando de lista de prohibiciones de rol

cmd-rolebanlist-desc = Lista las prohibiciones de rol del usuario
cmd-rolebanlist-help = Uso: <nombre o ID de usuario> [incluir desprohibidos]

## Sugerencias de resultados de completado
cmd-rolebanlist-hint-1 = <nombre o ID de usuario>
cmd-rolebanlist-hint-2 = [incluir desprohibidos]


cmd-roleban-minutes-parse = {$time} no es una cantidad de minutos válida.\n{$help}
cmd-roleban-severity-parse = ${severity} no es una gravedad válida\n{$help}.
cmd-roleban-arg-count = Cantidad de argumentos no válida.
cmd-roleban-job-parse = El trabajo {$job} no existe.
cmd-roleban-name-parse = No se puede encontrar un jugador con ese nombre.
cmd-roleban-existing = {$target} ya tiene una prohibición de rol para {$role}.
cmd-roleban-success = Se prohibió a {$target} del rol {$role} con la razón {$reason} {$length}.

cmd-roleban-inf = permanentemente
cmd-roleban-until =  hasta {$expires}

# Prohibiciones de departamento
cmd-departmentban-desc = Prohíbe a un jugador de los roles que comprenden un departamento
cmd-departmentban-help = Uso: departmentban <nombre o ID de usuario> <departamento> <razón> [duración en minutos, omitir o 0 para prohibición permanente]
