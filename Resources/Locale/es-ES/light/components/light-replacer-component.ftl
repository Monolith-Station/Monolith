
### Interaction Messages

# Mostrado cuando el jugador intenta reemplazar una luz, pero no quedan luces
comp-light-replacer-missing-light = No quedan luces en {THE($light-replacer)}.

# Mostrado cuando el jugador inserta una bombilla en el reemplazador de luces
comp-light-replacer-insert-light = Insertas {$bulb} en {THE($light-replacer)}.

# Mostrado cuando el jugador intenta insertar una bombilla rota en el reemplazador de luces
comp-light-replacer-insert-broken-light = ¡No puedes insertar luces rotas!

# Mostrado cuando el jugador recarga luces desde una caja de luces
comp-light-replacer-refill-from-storage = Recargas {THE($light-replacer)}.

### Examinar

comp-light-replacer-no-lights = Está vacío.
comp-light-replacer-has-lights = Contiene lo siguiente:
comp-light-replacer-light-listing = {$amount ->
    [one] [color=yellow]{$amount}[/color] [color=gray]{$name}[/color]
    *[other] [color=yellow]{$amount}[/color] [color=gray]{$name}s[/color]
}
