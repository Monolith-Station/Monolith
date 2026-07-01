## Jefe Rev

roles-antag-rev-head-name = Jefe Revolucionario
roles-antag-rev-head-objective = Tu objetivo es tomar el control de la estación convirtiendo a la gente a tu causa y eliminando a todo el personal de Mando en la estación.

head-rev-role-greeting =
    Eres un Jefe Revolucionario.
    Tienes la tarea de eliminar a todo el Mando de la estación mediante la muerte, el exilio o el encarcelamiento.
    El Syndicate te ha patrocinado con un flash que convierte a la tripulación a tu lado.
    Cuidado, esto no funcionará con Seguridad, Mando, o quienes lleven gafas de sol.
    ¡Viva la revolución!

head-rev-briefing =
    Usa flashes para convertir a la gente a tu causa.
    Elimina a todos los jefes para tomar el control de la estación.

head-rev-break-mindshield = ¡El Escudo Mental fue destruido!

## Rev

roles-antag-rev-name = Revolucionario
roles-antag-rev-objective = Tu objetivo es garantizar la seguridad y seguir las órdenes de los Jefes Revolucionarios, así como deshacerte de todo el personal de Mando en la estación.

rev-break-control = ¡{$name} ha recordado su verdadera lealtad!

rev-role-greeting =
    Eres un Revolucionario.
    Tienes la tarea de tomar el control de la estación y proteger a los Jefes Revolucionarios.
    Deshazte de todo el personal de Mando.
    ¡Viva la revolución!

rev-briefing = Ayuda a tus jefes revolucionarios a eliminar a cada jefe para tomar el control de la estación.

## General

rev-title = Revolucionarios
rev-description = Hay revolucionarios entre nosotros.

rev-not-enough-ready-players = No hay suficientes jugadores listos para la partida. Había {$readyPlayersCount} jugadores listos de los {$minimumPlayers} necesarios. No se puede iniciar una Revolución.
rev-no-one-ready = ¡No hay jugadores listos! No se puede iniciar una Revolución.
rev-no-heads = No había Jefes Revolucionarios para seleccionar. No se puede iniciar una Revolución.

rev-won = Los Jefes Rev sobrevivieron y tomaron el control de la estación con éxito.

rev-lost = El Mando sobrevivió y eliminó a todos los Jefes Rev.

rev-stalemate = Todos los Jefes Rev y el Mando murieron. Es un empate.

rev-reverse-stalemate = Tanto el Mando como los Jefes Rev sobrevivieron.

rev-headrev-count = {$initialCount ->
    [one] Hubo un Jefe Revolucionario:
    *[other] Hubo {$initialCount} Jefes Revolucionarios:
}

rev-headrev-name-user = [color=#5e9cff]{$name}[/color] ([color=gray]{$username}[/color]) convirtió a {$count} {$count ->
    [one] persona
    *[other] personas
}

rev-headrev-name = [color=#5e9cff]{$name}[/color] convirtió a {$count} {$count ->
    [one] persona
    *[other] personas
}

## Ventana de desconversión

rev-deconverted-title = ¡Desconvertido!
rev-deconverted-text =
    Al morir el último jefe rev, la revolución ha terminado.

    Ya no eres un revolucionario, así que sé amable.
rev-deconverted-confirm = Confirmar
