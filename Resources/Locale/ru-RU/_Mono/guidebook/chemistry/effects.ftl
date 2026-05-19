health-scale-display =
    { $deltasign ->
        [-1] { $kind } урон в [color=green]x{ $amount }[/color] раз
        [0] { $kind } урон в x{ $amount } раз
        [1] { $kind } урон в [color=red]x{ $amount }[/color] раз
       *[other] { $kind } урон в x{ $amount } раз
    }
reagent-effect-guidebook-health-scale =
    { $chance ->
        [1] Умножает { $changes }
       *[other] Имеет шанс { $chance }%, чтобы умножить { $changes }
    }
reagent-effect-guidebook-claws-growth =
    { $chance ->
        [1] Рост
       *[other] рост
    } когтей в { $amount }x раз пока метаболизируется
reagent-effect-guidebook-claws-growth-suppression =
    { $chance ->
        [1] Подавляет
       *[other] подавляет
    } рост когтей.
