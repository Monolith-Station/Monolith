health-scale-display =
    { $deltasign ->
        [-1] { $kind } damage by [color=green]x{ $amount }[/color]
         [0] { $kind } damage by x{ $amount }
         [1] { $kind } damage by [color=red]x{ $amount }[/color]
        *[other] { $kind } damage by x{ $amount }
    }

reagent-effect-guidebook-health-scale =
    { $chance ->
        [1] Multiplies existing { $changes }
       *[other] Has a { $chance }% chance to multiply existing { $changes }
    }

reagent-effect-guidebook-claws-growth =
    { $chance ->
        [1] Grows
        *[other] grow
    } claws at { $amount }x the speed while metabolizing

reagent-effect-guidebook-claws-growth-suppression =
    { $chance ->
        [1] Supresses
        *[other] supress
    } claws growth.