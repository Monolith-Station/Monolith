armor-plate-break = Your {$plateName} has shattered!
armor-plate-examine-with-plate = Has a [color=yellow]{$plateName}[/color] installed. Durability: [color={$durabilityColor}]{$percent}%[/color]
armor-plate-examine-with-plate-simple = Has a [color=yellow]{$plateName}[/color] installed.
armor-plate-examine-no-plate = No armor plate installed.
armor-plate-examine-no-storage = No storage compartment for armor plates.

armor-plate-examinable-verb-text = Plate attributes
armor-plate-examinable-verb-message = Examine protection and durability characteristics.

armor-plate-attributes-examine = This armor plate:
armor-plate-initial-durability = Is rated for [color=yellow]{ $durability }[/color] standard units of damage.

armor-plate-item-durability = Durability: [color={$durabilityColor}]{$percent}%[/color]

armor-plate-gait-speed = speed
armor-plate-gait-walk = walking speed
armor-plate-gait-sprint = running speed

armor-plate-speed-display =
    { $stringClause ->
         [1] Increases your {$gait} by [color=yellow]{$speedPercent}%[/color].
         [-1] Decreases your {$gait} by [color=yellow]{$speedPercent}%[/color].
        *[other] Shouldn't be have this speed clause!
    }

armor-plate-ratios-display =
    { $stringClause ->
        [1] [color=cyan]Absorbs[/color] [color=yellow]{$ratioPercent}%[/color] of [color=yellow]{$dmgType}[/color]
        [-1] [color=fuchsia]Amplifies[/color] [color=yellow]{$dmgType}[/color] by [color=yellow]{$ratioPercent}%[/color]
        [0] Does not affect [color=yellow]{$dmgType}[/color]
       *[other] {$dmgType} shouldn't have this absorption clause!
    }

armor-plate-multiplier-display = and deducts [color=yellow]{$multiplier}%[/color] of raw damage value from durability.
armor-plate-multiplier-none = and does not damage the plate.

armor-plate-stamina-source-absorb = [color=cyan]Absorbed[/color]
armor-plate-stamina-concat = and
armor-plate-stamina-source-amplified = [color=fuchsia]Amplified[/color]
armor-plate-stamina-source-raw = [color=red]All Oncoming[/color]
armor-plate-stamina-value = Inflicts [color=yellow]{$multiplier}%[/color] of {$sources} damage as stamina damage.

