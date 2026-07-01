### Interaction Messages

# Se muestra al reparar algo
comp-repairable-repair = Reparas {PROPER($target) ->
  [true] {""}
  *[false] el{" "}
}{$target} con {PROPER($tool) ->
  [true] {""}
  *[false] el{" "}
}{$tool}
