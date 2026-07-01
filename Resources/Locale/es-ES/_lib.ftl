### Mensajes especiales usados internamente por el localizador.

# Usado internamente por la función PRESSURE().
# Frontier: PBa<PPa
zzzz-fmt-pressure = { TOSTRING($divided, "F1") } { $places ->
    [0] kPa
    [1] MPa
    [2] GPa
    [3] TPa
    [4] PPa
    *[5] ???
}

# Usado internamente por la función POWERWATTS().
zzzz-fmt-power-watts = { TOSTRING($divided, "F1") } { $places ->
    [0] W
    [1] kW
    [2] MW
    [3] GW
    [4] TW
    *[5] ???
}

# Usado internamente por la función POWERJOULES().
# Recordatorio: 1 julio = 1 vatio por 1 segundo (multiplica vatios por segundos para obtener julios).
# Por lo tanto, 1 kilovatio-hora equivale a 3.600.000 julios (3,6 MJ)
zzzz-fmt-power-joules = { TOSTRING($divided, "F1") } { $places ->
    [0] J
    [1] kJ
    [2] MJ
    [3] GJ
    [4] TJ
    *[5] ???
}

# Usado internamente por la función ENERGYWATTHOURS().
zzzz-fmt-energy-watt-hours = { TOSTRING($divided, "F1") } { $places ->
    [0] Wh
    [1] kWh
    [2] MWh
    [3] GWh
    [4] TWh
    *[5] ???
}

# Usado internamente por la función PLAYTIME().
zzzz-fmt-playtime = {$hours}H {$minutes}M
