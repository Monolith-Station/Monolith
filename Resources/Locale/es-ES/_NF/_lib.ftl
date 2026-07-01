### Mensajes especiales utilizados por el localizador interno.

# Utilizado internamente por la función GASQUANTITY().
zzzz-fmt-gas-quantity = { TOSTRING($divided, "F1") } { $places ->
    [0] mol
    [1] kmol
    [2] Mmol
    [3] Gmol
    [4] Tmol
    *[5] ???
}
