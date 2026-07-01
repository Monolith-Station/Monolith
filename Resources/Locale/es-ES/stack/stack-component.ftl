### UI

# Mostrado cuando se examina una pila en rango de detalle
comp-stack-examine-detail-count = {$count ->
    [one] Hay [color={$markupCountColor}]{$count}[/color] objeto
    *[other] Hay [color={$markupCountColor}]{$count}[/color] objetos
} en la pila.

# Control de estado de la pila
comp-stack-status = Cantidad: [color=white]{$count}[/color]

### Mensajes de Interacción

# Mostrado al intentar añadir a una pila que está llena
comp-stack-already-full = La pila ya está llena.

# Mostrado cuando una pila se llena
comp-stack-becomes-full = La pila está ahora llena.

# Texto relacionado con dividir una pila
comp-stack-split = Has dividido la pila.
comp-stack-split-halve = Dividir a la mitad
comp-stack-split-custom = Cantidad a dividir...
comp-stack-split-too-small = La pila es demasiado pequeña para dividir.

# Cherry-picked de space-station-14#32938 cortesía de Ilya246
comp-stack-split-size = Máx: {$size}

ui-custom-stack-split-title = Dividir Cantidad
ui-custom-stack-split-line-edit-placeholder = Cantidad
ui-custom-stack-split-apply = Dividir
# Fin cherry-pick de ss14#32938
