device-pda-slot-component-slot-name-cartridge = Cartucho

default-program-name = Programa
notekeeper-program-name = Anotador
news-read-program-name = Noticias de la estación

crew-manifest-program-name = Manifiesto de tripulación
crew-manifest-cartridge-loading = Cargando ...

net-probe-program-name = NetProbe
net-probe-scan = ¡{$device} escaneado!
net-probe-label-name = Nombre
net-probe-label-address = Dirección
net-probe-label-frequency = Frecuencia
net-probe-label-network = Red

log-probe-program-name = LogProbe
log-probe-scan = ¡Registros descargados de {$device}!
log-probe-label-time = Hora
log-probe-label-accessor = Accedido por
log-probe-label-number = #
log-probe-print-button = Imprimir registros
log-probe-printout-device = Dispositivo escaneado: {$name}
log-probe-printout-header = Registros más recientes:
log-probe-printout-entry = #{$number} / {$time} / {$accessor}

astro-nav-program-name = AstroNav

med-tek-program-name = MedTek

# Cartucho de lista de buscados
wanted-list-program-name = Lista de buscados
wanted-list-label-no-records = Todo bien, vaquero
wanted-list-search-placeholder = Buscar por nombre y estado

wanted-list-age-label = [color=darkgray]Edad:[/color] [color=white]{$age}[/color]
wanted-list-job-label = [color=darkgray]Trabajo:[/color] [color=white]{$job}[/color]
wanted-list-species-label = [color=darkgray]Especie:[/color] [color=white]{$species}[/color]
wanted-list-gender-label = [color=darkgray]Género:[/color] [color=white]{$gender}[/color]

wanted-list-reason-label = [color=darkgray]Motivo:[/color] [color=white]{$reason}[/color]
wanted-list-unknown-reason-label = motivo desconocido

wanted-list-initiator-label = [color=darkgray]Iniciador:[/color] [color=white]{$initiator}[/color]
wanted-list-unknown-initiator-label = iniciador desconocido

wanted-list-status-label = [color=darkgray]estado:[/color] {$status ->
        [suspected] [color=yellow]sospechoso[/color]
        [wanted] [color=red]buscado[/color]
        [detained] [color=#b18644]detenido[/color]
        [paroled] [color=green]en libertad condicional[/color]
        [discharged] [color=green]dado de baja[/color]
        *[other] ninguno
    }

wanted-list-history-table-time-col = Hora
wanted-list-history-table-reason-col = Delito
wanted-list-history-table-initiator-col = Iniciador
