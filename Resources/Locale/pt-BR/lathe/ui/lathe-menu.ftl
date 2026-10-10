lathe-menu-title= Menu do Torno
lathe-menu-queue= Fila
lathe-menu-server-list= Lista de servidores
lathe-menu-sync= Sincronizar
lathe-menu-search-designs= Pesquisar projetos
lathe-menu-category-all= Todos
lathe-menu-search-filter= Filtro:
lathe-menu-amount= Quantidade:
lathe-menu-loop= Repetir
lathe-menu-skip= Pular Se Insuficiente
lathe-menu-reagent-slot-examine= Tem um slot para um béquer no lado.
lathe-reagent-dispense-no-container= O líquido escapa de {THE($name)} no chão!
lathe-menu-result-reagent-display= {$reagent} ({$amount}u)
lathe-menu-material-display= {$material} ({$amount})
lathe-menu-tooltip-display= {$amount} de {$material}
lathe-menu-description-display= [italic]{$description}[/italic]
lathe-menu-material-amount= { $amount ->
    [1] {NATURALFIXED($amount, 2)} {$unit}
    *[other] {NATURALFIXED($amount, 2)} {MAKEPLURAL($unit)}
}
lathe-menu-material-amount-missing= { $amount ->
    [1] {NATURALFIXED($amount, 2)} {$unit} de {$material} ([color=red]{NATURALFIXED($missingAmount, 2)} {$unit} faltando[/color])
    *[other] {NATURALFIXED($amount, 2)} {MAKEPLURAL($unit)} de {$material} ([color=red]{NATURALFIXED($missingAmount, 2)} {MAKEPLURAL($unit)} faltando[/color])
}
lathe-menu-entity-amount-missing = {$amount} of {$material} ([color=red]{$missingAmount} missing[/color])
lathe-menu-reagent-amount-missing = {$amount}u of {$material} ([color=red]{$missingAmount}u missing[/color])
lathe-menu-no-materials-message= Nenhum material carregado.
lathe-menu-silo-linked-message= Silo Conectado
lathe-menu-fabricating-message= Fabricando...
lathe-menu-materials-title= Materiais
lathe-menu-queue-title= Fila de Produção
