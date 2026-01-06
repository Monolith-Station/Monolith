armor-plate-break = Your {$plateName} has shattered!
armor-plate-examine-with-plate = Has a [color=yellow]{$plateName}[/color] installed. Durability: [color={$durabilityColor}]{$percent}%[/color]
armor-plate-examine-with-plate-simple = Has a [color=yellow]{$plateName}[/color] installed.
armor-plate-examine-no-plate = No armor plate installed.
armor-plate-examine-no-storage = No storage compartment for armor plates.

armor-plate-examinable-verb-text = Plate attributes
armor-plate-examinable-verb-message = Examine protection and durability characteristics.
armor-plate-sprint = Sprint speed modifier: {sprint}

armor-plate-attributes-examine = This armor plate:
armor-plate-initial-durability = Is rated for [color=yellow]{ $durability }[/color] standard units of damage.

plate-speed-increase-equal-examine = Increases your speed by [color=yellow]{$walkSpeed}%[/color].
plate-speed-decrease-equal-examine = Decreases your speed by [color=yellow]{$walkSpeed}%[/color].
armor-plate-speed-increase-equal-examine = Increases your speed by [color=yellow]{$walkSpeed}%[/color].
armor-plate-speed-decrease-equal-examine = Decreases your speed by [color=yellow]{$walkSpeed}%[/color].
armor-plate-speed-increase-run-examine = Increases your running speed by [color=yellow]{$runSpeed}%[/color].
armor-plate-speed-decrease-run-examine = Decreases your running speed by [color=yellow]{$runSpeed}%[/color].
armor-plate-speed-increase-walk-examine = Increases your walking speed by [color=yellow]{$walkSpeed}%[/color].
armor-plate-speed-decrease-walk-examine = Decreases your walking speed by [color=yellow]{$walkSpeed}%[/color].

armor-plate-ratios-display =
    { $deltasign ->
        [-1] [color=cyan]Absorbs[/color] [color=yellow]{$ratioPercent}%[/color] of [color=yellow]{$dmgType}[/color] and takes it as [color=yellow]x{$multiplier}[/color] durability damage.
         [0] Is unaffected by {$dmgType}
         [1] [color=fuchsia]Amplifies[/color] [color=yellow]{$dmgType}[/color] by [color=yellow]{$ratioPercent}%[/color] and takes the added damage as [color=yellow]x{$multiplier}[/color] durability damage.
        *[other] {$dmgType} shouldn't be have this absorption value!
    }
armor-plate-stamina-value = Inflicts [color=yellow]{$multiplier}%[/color] of absorbed damage as stamina damage.
