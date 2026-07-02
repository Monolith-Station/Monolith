## Superviviente

roles-antag-survivor-name = Superviviente
# Es una referencia a Halo
roles-antag-survivor-objective = Objetivo Actual: Sobrevivir

survivor-role-greeting =
    Eres un Superviviente.
    Ante todo necesitas regresar a CentComm con vida.
    Recoge todo el poder de fuego necesario para garantizar tu supervivencia.
    No confíes en nadie.

survivor-round-end-dead-count =
{
    $deadCount ->
        [one] [color=red]{$deadCount}[/color] superviviente murió.
        *[other] [color=red]{$deadCount}[/color] supervivientes murieron.
}

survivor-round-end-alive-count =
{
    $aliveCount ->
        [one] [color=yellow]{$aliveCount}[/color] superviviente quedó varado en la estación.
        *[other] [color=yellow]{$aliveCount}[/color] supervivientes quedaron varados en la estación.
}

survivor-round-end-alive-on-shuttle-count =
{
    $aliveCount ->
        [one] [color=green]{$aliveCount}[/color] superviviente logró salir con vida.
        *[other] [color=green]{$aliveCount}[/color] supervivientes lograron salir con vida.
}

## Mago

objective-issuer-swf = [color=turquoise]The Space Wizards Federation[/color]

wizard-title = Mago
wizard-description = ¡Hay un Mago en la estación! Nunca se sabe qué podría hacer.

roles-antag-wizard-name = Mago
roles-antag-wizard-objective = Enséñales una lección que nunca olvidarán.

wizard-role-greeting =
    ¡ERES UN MAGO!
    Ha habido tensiones entre la Space Wizards Federation y NanoTrasen.
    Así que la Space Wizards Federation te ha seleccionado para visitar la estación.
    Dales una buena demostración de tus poderes.
    Lo que hagas depende de ti, pero recuerda que la Space Wizards Federation quiere que salgas con vida.

wizard-round-end-name = mago

## TODO: Mago Aprendiz (Llegará en algún momento tras el lanzamiento del mago)
