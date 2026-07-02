gas-deposit-drill-no-resources = ¡No hay nada que extraer aquí!
gas-deposit-drill-system-examined = El extractor está configurado a [color={$statusColor}]{PRESSURE($pressure)}[/color].
gas-deposit-drill-system-examined-amount = El extractor informa de {
    $value ->
        [0] [color={$statusColor}]casi nada[/color] restante.
        *[other] aproximadamente [color={$statusColor}]{GASQUANTITY($value)}[/color] restante.
    }
gas-deposit-drill-system-examined-yield = El extractor informa de que [color={$statusColor}]{NATURALFIXED($yield, 1)}%[/color]{
    $hitMinimum ->
        [false] de rendimiento queda.
        *[other] de rendimiento queda, y se han alcanzado las reservas profundas.
    }
