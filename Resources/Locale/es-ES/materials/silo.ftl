ore-silo-ui-title = Silo de Materiales
ore-silo-ui-label-clients = Máquinas
ore-silo-ui-label-mats = Materiales
ore-silo-ui-itemlist-entry = {$linked ->
[true] {"[Vinculado] "}
*[False] {""}
} {$name} ({$beacon}) {$inRange ->
[true] {""}
*[false] (Fuera de rango)
}
