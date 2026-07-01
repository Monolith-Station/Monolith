generator-clogged = ¡{CAPITALIZE(THE($generator))} se apaga de forma abrupta!

portable-generator-verb-start = Arrancar generador
portable-generator-verb-start-msg-unreliable = Arrancar el generador. Puede que necesite varios intentos.
portable-generator-verb-start-msg-reliable = Arrancar el generador.
portable-generator-verb-start-msg-unanchored = ¡El generador debe estar anclado primero!
portable-generator-verb-stop = Detener generador
portable-generator-start-fail = Tiras de la cuerda, pero no arrancó.
portable-generator-start-success = Tiras de la cuerda y cobra vida con un zumbido.

portable-generator-ui-title = Generador Portátil
portable-generator-ui-status-stopped = Detenido:
portable-generator-ui-status-starting = Arrancando:
portable-generator-ui-status-running = En marcha:
portable-generator-ui-start = Arrancar
portable-generator-ui-stop = Detener
portable-generator-ui-target-power-label = Potencia objetivo (kW):
portable-generator-ui-efficiency-label = Eficiencia:
portable-generator-ui-fuel-use-label = Consumo de combustible:
portable-generator-ui-fuel-left-label = Combustible restante:
portable-generator-ui-clogged = ¡Contaminantes detectados en el depósito de combustible!
portable-generator-ui-eject = Expulsar
portable-generator-ui-eta = (~{ $minutes } min)
portable-generator-ui-unanchored = Sin anclar
portable-generator-ui-current-output = Salida actual: {$voltage}
portable-generator-ui-network-stats = Red:
portable-generator-ui-network-stats-value = { POWERWATTS($supply) } / { POWERWATTS($load) }
portable-generator-ui-network-stats-not-connected = Sin conexión

power-switchable-generator-examine = La salida de energía está configurada en {$voltage}.
power-switchable-generator-switched = ¡Salida cambiada a {$voltage}!

power-switchable-voltage = { $voltage ->
    [HV] [color=orange]HV[/color]
    [MV] [color=yellow]MV[/color]
    *[LV] [color=green]LV[/color]
}
power-switchable-switch-voltage = Cambiar a {$voltage}

fuel-generator-verb-disable-on = ¡Apaga el generador primero!
