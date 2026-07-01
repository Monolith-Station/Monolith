# Loading Screen

replay-loading = Cargando ({$cur}/{$total})
replay-loading-reading = Leyendo archivos
replay-loading-processing = Procesando archivos
replay-loading-spawning = Generando entidades
replay-loading-initializing = Inicializando entidades
replay-loading-starting= Iniciando entidades
replay-loading-failed = Error al cargar la repetición. Error:
                        {$reason}
replay-loading-retry = Intentar cargar con mayor tolerancia a excepciones - ¡PUEDE CAUSAR ERRORES!
replay-loading-cancel = Cancelar

# Main Menu
replay-menu-subtext = Cliente de repetición
replay-menu-load = Cargar repetición seleccionada
replay-menu-select = Seleccionar una repetición
replay-menu-open = Abrir carpeta de repeticiones
replay-menu-none = No se encontraron repeticiones.

# Main Menu Info Box
replay-info-title = Información de la repetición
replay-info-none-selected = No hay ninguna repetición seleccionada
replay-info-invalid = [color=red]Repetición seleccionada no válida[/color]
replay-info-info = {"["}color=gray]Seleccionado:[/color]  {$name} ({$file})
                   {"["}color=gray]Hora:[/color]   {$time}
                   {"["}color=gray]ID de partida:[/color]   {$roundId}
                   {"["}color=gray]Duración:[/color]   {$duration}
                   {"["}color=gray]ID de versión:[/color]   {$forkId}
                   {"["}color=gray]Versión:[/color]   {$version}
                   {"["}color=gray]Motor:[/color]   {$engVersion}
                   {"["}color=gray]Hash de tipo:[/color]   {$hash}
                   {"["}color=gray]Hash de comp.:[/color]   {$compHash}

# Replay selection window
replay-menu-select-title = Seleccionar repetición

# Replay related verbs
replay-verb-spectate = Espectador

# command
cmd-replay-spectate-help = replay_spectate [entidad opcional]
cmd-replay-spectate-desc = Conecta o desconecta al jugador local de una entidad con el uid indicado.
cmd-replay-spectate-hint = EntityUid opcional

cmd-replay-toggleui-desc = Activa o desactiva la interfaz de control de la repetición.
