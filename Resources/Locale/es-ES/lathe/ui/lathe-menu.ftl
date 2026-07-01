lathe-menu-title = Menú del Torno
lathe-menu-queue = Cola
lathe-menu-server-list = Lista de servidores
lathe-menu-sync = Sincronizar
lathe-menu-search-designs = Buscar diseños
lathe-menu-category-all = Todo
lathe-menu-search-filter = Filtrar:
lathe-menu-amount = Cantidad:
lathe-menu-loop = Bucle
lathe-menu-skip = Omitir si es insuficiente
lathe-menu-reagent-slot-examine = Tiene una ranura para un vaso de precipitados en el lateral.
lathe-reagent-dispense-no-container = ¡El líquido se derrama de {THE($name)} al suelo!
lathe-menu-result-reagent-display = {$reagent} ({$amount}u)
lathe-menu-material-display = {$material} ({$amount})
lathe-menu-tooltip-display = {$amount} de {$material}
lathe-menu-description-display = [italic]{$description}[/italic]
lathe-menu-material-amount = { $amount ->
    [1] {NATURALFIXED($amount, 2)} {$unit}
    *[other] {NATURALFIXED($amount, 2)} {MAKEPLURAL($unit)}
}
lathe-menu-material-amount-missing = { $amount ->
    [1] {NATURALFIXED($amount, 2)} {$unit} de {$material} ([color=red]{NATURALFIXED($missingAmount, 2)} {$unit} que faltan[/color])
    *[other] {NATURALFIXED($amount, 2)} {MAKEPLURAL($unit)} de {$material} ([color=red]{NATURALFIXED($missingAmount, 2)} {MAKEPLURAL($unit)} que faltan[/color])
}
lathe-menu-no-materials-message = No hay materiales cargados.
lathe-menu-silo-linked-message = Silo conectado
lathe-menu-fabricating-message = Fabricando...
lathe-menu-materials-title = Materiales
lathe-menu-queue-title = Cola de fabricación
