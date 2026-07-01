game-ticker-restart-round = Reiniciando partida...
game-ticker-start-round = La partida comienza ahora...
game-ticker-start-round-cannot-start-game-mode-fallback = ¡Error al iniciar el modo {$failedGameMode}! Cambiando al modo {$fallbackMode}...
game-ticker-start-round-cannot-start-game-mode-restart = ¡Error al iniciar el modo {$failedGameMode}! Reiniciando partida...
game-ticker-start-round-invalid-map = El mapa seleccionado {$map} no es apto para el modo de juego {$mode}. Es posible que el modo de juego no funcione como se esperaba...
game-ticker-unknown-role = Desconocido
game-ticker-delay-start = El inicio de la partida se ha retrasado {$seconds} segundos.
game-ticker-pause-start = El inicio de la partida ha sido pausado.
game-ticker-pause-start-resumed = La cuenta atrás del inicio de la partida ha sido reanudada.
game-ticker-player-join-game-message = ¡Bienvenido a Space Station 14! Si es tu primera vez jugando, asegúrate de leer las reglas del juego y no tengas miedo de pedir ayuda en LOOC (OOC local) u OOC (generalmente disponible solo entre partidas).
game-ticker-get-info-text = Hola y bienvenido a [color=white]Space Station 14![/color]
                            La partida actual es: [color=white]#{$roundId}[/color]
                            El número de jugadores actuales es: [color=white]{$playerCount}[/color]
                            El mapa actual es: [color=white]{$mapName}[/color]
                            El modo de juego actual es: [color=white]{$gmTitle}[/color]
                            >[color=yellow]{$desc}[/color]
game-ticker-get-info-preround-text = Hola y bienvenido a [color=white]Space Station 14![/color]
                            La partida actual es: [color=white]#{$roundId}[/color]
                            El número de jugadores actuales es: [color=white]{$playerCount}[/color] ([color=white]{$readyCount}[/color] {$readyCount ->
                                [one] está listo
                                *[other] están listos
                            })
                            El mapa actual es: [color=white]{$mapName}[/color]
                            El modo de juego actual es: [color=white]{$gmTitle}[/color]
                            >[color=yellow]{$desc}[/color]
game-ticker-no-map-selected = [color=yellow]¡Mapa aún no seleccionado![/color]
game-ticker-player-no-jobs-available-when-joining = Al intentar unirse al juego, no había trabajos disponibles.

# Se muestra en el chat a los administradores cuando un jugador se une
player-join-message = El jugador {$name} se ha unido.
player-first-join-message = El jugador {$name} se ha unido por primera vez.

# Se muestra en el chat a los administradores cuando un jugador se va
player-leave-message = El jugador {$name} se ha ido.

latejoin-arrival-announcement = {$character} ({$job}) { CONJUGATE-HAVE($entity) } llegado a la estación!
latejoin-arrival-announcement-special = ¡{$job} {$character} en cubierta!
latejoin-arrival-sender = Estación
latejoin-arrivals-direction = Un transbordador que te llevará a tu estación llegará en breve.
latejoin-arrivals-direction-time = Un transbordador que te llevará a tu estación llegará en {$time}.
latejoin-arrivals-dumped-from-shuttle = Una fuerza misteriosa te impide salir con el transbordador de llegadas.
latejoin-arrivals-teleport-to-spawn = Una fuerza misteriosa te teletransporta fuera del transbordador de llegadas. ¡Que tengas un turno seguro!

preset-not-enough-ready-players = No se puede iniciar {$presetName}. Requiere {$minimumPlayers} jugadores pero solo hay {$readyPlayersCount}.
preset-no-one-ready = No se puede iniciar {$presetName}. Ningún jugador está listo.

game-run-level-PreRoundLobby = Sala de espera previa
game-run-level-InRound = En partida
game-run-level-PostRound = Partida terminada
