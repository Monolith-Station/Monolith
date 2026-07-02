# Mostrado como iniciador de la votación cuando ningún usuario la crea
ui-vote-initiator-server = El servidor

## Default.Votes

ui-vote-restart-title = Reiniciar partida
ui-vote-restart-succeeded = La votación de reinicio fue exitosa.
ui-vote-restart-failed = La votación de reinicio falló (se necesita { TOSTRING($ratio, "P0") }).
ui-vote-restart-fail-not-enough-ghost-players = La votación de reinicio falló: Se requiere un mínimo del { $ghostPlayerRequirement }% de jugadores fantasma para iniciar una votación de reinicio. Actualmente, no hay suficientes jugadores fantasma.
ui-vote-restart-yes = Sí
ui-vote-restart-no = No
ui-vote-restart-abstain = Abstención

ui-vote-gamemode-title = Próximo modo de juego
ui-vote-gamemode-tie = ¡Empate en la votación de modo de juego! Eligiendo... { $picked }
ui-vote-gamemode-win = ¡{ $winner } ganó la votación de modo de juego!

ui-vote-map-title = Próximo mapa
ui-vote-map-tie = ¡Empate en la votación de mapa! Eligiendo... { $picked }
ui-vote-map-win = ¡{ $winner } ganó la votación de mapa!
ui-vote-map-notlobby = ¡La votación de mapas solo es válida en el vestíbulo previo a la partida!
ui-vote-map-notlobby-time = ¡La votación de mapas solo es válida en el vestíbulo previo a la partida con { $time } restante!


# Votos de expulsión
ui-vote-votekick-unknown-initiator = Un jugador
ui-vote-votekick-unknown-target = Jugador desconocido
ui-vote-votekick-title = { $initiator } ha iniciado una votación de expulsión para el usuario: { $targetEntity }. Motivo: { $reason }
ui-vote-votekick-yes = Sí
ui-vote-votekick-no = No
ui-vote-votekick-abstain = Abstención
ui-vote-votekick-success = La votación de expulsión para { $target } fue exitosa. Motivo de expulsión: { $reason }
ui-vote-votekick-failure = La votación de expulsión para { $target } falló. Motivo de expulsión: { $reason }
ui-vote-votekick-not-enough-eligible = No hay suficientes votantes elegibles en línea para iniciar una votación de expulsión: { $voters }/{ $requirement }
ui-vote-votekick-server-cancelled = La votación de expulsión para { $target } fue cancelada por el servidor.
