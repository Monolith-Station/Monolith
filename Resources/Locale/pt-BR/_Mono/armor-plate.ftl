armor-plate-break = Sua {$plateName} foi quebrada!
armor-plate-examine-with-plate = Tem uma [color=yellow]{$plateName}[/color] instalada. Durabilidade: [color={$durabilityColor}]{$percent}%[/color]
armor-plate-examine-with-plate-simple = Tem uma [color=yellow]{$plateName}[/color] instalada.
armor-plate-examine-no-plate = Nenhuma placa de blindagem instalada.
armor-plate-examine-no-storage = Nenhum compartimento de armazenamento para placas de blindagem.

armor-plate-examinable-verb-text = Atributos da placa
armor-plate-examinable-verb-message = Examinar características de proteção e durabilidade.

armor-plate-attributes-examine = Essa placa de blindagem:
armor-plate-initial-durability = É classificada para [color=yellow]{ $durability }[/color] unidades padrão de dano.

armor-plate-item-durability = Durabilidade: [color={$durabilityColor}]{$percent}%[/color]

armor-plate-gait-speed = velocidade
armor-plate-gait-walk = velocidade de caminhada
armor-plate-gait-sprint = velocidade de corrida

armor-plate-speed-display =
    { $stringClause ->
        [-1] Aumenta sua {$gait} em [color=yellow]{$speedPercent}%[/color].
         [1] Diminui sua {$gait} em [color=yellow]{$speedPercent}%[/color].
        *[other] Não deveria ter este valor de velocidade!
    }

armor-plate-ratios-display =
    { $stringClause ->
        [1] [color=cyan]Absorve[/color] [color=yellow]{$ratioPercent}%[/color] de dano [color=yellow]{$dmgType}[/color]
        [-1] [color=fuchsia]Amplifica[/color] dano [color=yellow]{$dmgType}[/color] por [color=yellow]{$ratioPercent}%[/color]
        [0] Não afeta [color=yellow]{$dmgType}[/color]
       *[other] {$dmgType} não deveria ter esse valor!
    }

armor-plate-multiplier-display = e transforma em dano de durabilidade [color=yellow]{$multiplier}%[/color] do dano pré redução.
armor-plate-multiplier-none = e não danifica a placa.

armor-plate-stamina-source-absorb = [color=cyan]Absorvido[/color]
armor-plate-stamina-concat = e
armor-plate-stamina-source-amplified = [color=fuchsia]Amplificado[/color]
armor-plate-stamina-source-raw = [color=red]Todos[/color]
armor-plate-stamina-value = Inflinge [color=yellow]{$multiplier}%[/color] de dano {$sources} como dano de stamina.
