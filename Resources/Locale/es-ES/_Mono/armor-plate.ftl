armor-plate-break = ¡Tu {$plateName} se ha hecho añicos!
armor-plate-examine-with-plate = Tiene una [color=yellow]{$plateName}[/color] instalada. Durabilidad: [color={$durabilityColor}]{$percent}%[/color]
armor-plate-examine-with-plate-simple = Tiene una [color=yellow]{$plateName}[/color] instalada.
armor-plate-examine-no-plate = No hay placa de armadura instalada.
armor-plate-examine-no-storage = No hay compartimento de almacenamiento para placas de armadura.

armor-plate-examinable-verb-text = Atributos de la placa
armor-plate-examinable-verb-message = Examinar las características de protección y durabilidad.

armor-plate-attributes-examine = Esta placa de armadura:
armor-plate-initial-durability = Está valorada para [color=yellow]{ $durability }[/color] unidades estándar de daño.

armor-plate-item-durability = Durabilidad: [color={$durabilityColor}]{$percent}%[/color]

armor-plate-gait-speed = velocidad
armor-plate-gait-walk = velocidad al caminar
armor-plate-gait-sprint = velocidad al correr

armor-plate-speed-display =
    { $deltasign ->
        [-1] Aumenta tu {$gait} en [color=yellow]{$speedPercent}%[/color].
         [0] No afecta tu velocidad.
         [1] Reduce tu {$gait} en [color=yellow]{$speedPercent}%[/color].
        *[other] ¡No debería tener este valor de velocidad!
    }

armor-plate-ratios-display =
    { $deltasign ->
        [-1] [color=cyan]Absorbe[/color] [color=yellow]{$ratioPercent}%[/color] de [color=yellow]{$dmgType}[/color] y lo toma como [color=yellow]x{$multiplier}[/color] de daño a la durabilidad.
         [0] No se ve afectada por {$dmgType}
         [1] [color=fuchsia]Amplifica[/color] [color=yellow]{$dmgType}[/color] en [color=yellow]{$ratioPercent}%[/color] y toma el daño adicional como [color=yellow]x{$multiplier}[/color] de daño a la durabilidad.
        *[other] ¡{$dmgType} no debería tener este valor de absorción!
    }
armor-plate-stamina-value = Inflige [color=yellow]{$multiplier}%[/color] del daño absorbido como daño de resistencia.
