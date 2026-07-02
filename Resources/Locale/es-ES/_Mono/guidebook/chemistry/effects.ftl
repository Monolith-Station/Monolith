health-scale-display =
    { $deltasign ->
        [-1] { $kind } daño por [color=green]x{ $amount }[/color]
         [0] { $kind } daño por x{ $amount }
         [1] { $kind } daño por [color=red]x{ $amount }[/color]
        *[other] { $kind } daño por x{ $amount }
    }

reagent-effect-guidebook-health-scale =
    { $chance ->
        [1] Multiplica los { $changes } existentes
       *[other] Tiene un { $chance }% de probabilidad de multiplicar los { $changes } existentes
    }

reagent-effect-guidebook-claws-growth =
    { $chance ->
        [1] Hace crecer
        *[other] hace crecer
    } garras a { $amount }x de velocidad mientras metaboliza

reagent-effect-guidebook-claws-growth-suppression =
    { $chance ->
        [1] Suprime
        *[other] suprime
    } el crecimiento de garras.
