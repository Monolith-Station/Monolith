discord-watchlist-connection-header =
    { $players ->
        [one] {$players} jugador en una lista de vigilancia se ha
        *[other] {$players} jugadores en una lista de vigilancia se han
    } conectado a {$serverName}

discord-watchlist-connection-entry = - {$playerName} con el mensaje "{$message}"{ $expiry ->
        [0] {""}
        *[other] {" "}(expira <t:{$expiry}:R>)
    }{ $otherWatchlists ->
        [0] {""}
        [one] {" "}y {$otherWatchlists} lista de vigilancia adicional
        *[other] {" "}y {$otherWatchlists} listas de vigilancia adicionales
    }
