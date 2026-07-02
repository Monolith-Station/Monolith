anomaly-component-contact-damage = ¡La anomalía te quema la piel!

anomaly-vessel-component-anomaly-assigned = Anomalía asignada al recipiente.
anomaly-vessel-component-not-assigned = Este recipiente no está asignado a ninguna anomalía. Intenta usar un escáner en él.
anomaly-vessel-component-assigned = Este recipiente está actualmente asignado a una anomalía.
anomaly-vessel-component-upgrade-output = producción de puntos

anomaly-particles-delta = Partículas delta
anomaly-particles-epsilon = Partículas épsilon
anomaly-particles-zeta = Partículas zeta
anomaly-particles-omega = Partículas omega
anomaly-particles-sigma = Partículas sigma

anomaly-scanner-component-scan-complete = ¡Escaneo completo!

anomaly-scanner-ui-title = escáner de anomalías
anomaly-scanner-no-anomaly = Ninguna anomalía escaneada actualmente.
anomaly-scanner-severity-percentage = Gravedad actual: [color=gray]{$percent}[/color]
anomaly-scanner-severity-percentage-unknown = Gravedad actual: [color=red]ERROR[/color]
anomaly-scanner-stability-low = Estado de anomalía actual: [color=gold]Decayendo[/color]
anomaly-scanner-stability-medium = Estado de anomalía actual: [color=forestgreen]Estable[/color]
anomaly-scanner-stability-high = Estado de anomalía actual: [color=crimson]Creciendo[/color]
anomaly-scanner-stability-unknown = Estado de anomalía actual: [color=red]ERROR[/color]
anomaly-scanner-point-output = Producción de puntos: [color=gray]{$point}[/color]
anomaly-scanner-point-output-unknown = Producción de puntos: [color=red]ERROR[/color]
anomaly-scanner-particle-readout = Análisis de Reacción de Partículas:
anomaly-scanner-particle-danger = - [color=crimson]Tipo peligroso:[/color] {$type}
anomaly-scanner-particle-unstable = - [color=plum]Tipo inestable:[/color] {$type}
anomaly-scanner-particle-containment = - [color=goldenrod]Tipo de contención:[/color] {$type}
anomaly-scanner-particle-transformation = - [color=#6b75fa]Tipo de transformación:[/color] {$type}
anomaly-scanner-particle-danger-unknown = - [color=crimson]Tipo peligroso:[/color] [color=red]ERROR[/color]
anomaly-scanner-particle-unstable-unknown = - [color=plum]Tipo inestable:[/color] [color=red]ERROR[/color]
anomaly-scanner-particle-containment-unknown = - [color=goldenrod]Tipo de contención:[/color] [color=red]ERROR[/color]
anomaly-scanner-particle-transformation-unknown = - [color=#6b75fa]Tipo de transformación:[/color] [color=red]ERROR[/color]
anomaly-scanner-pulse-timer = Tiempo hasta el próximo pulso: [color=gray]{$time}[/color]

anomaly-gorilla-core-slot-name = Núcleo de anomalía
anomaly-gorilla-charge-none = No tiene ningún [bold]núcleo de anomalía[/bold] en su interior.
anomaly-gorilla-charge-limit = Tiene [color={$count ->
    [3]green
    [2]yellow
    [1]orange
    [0]red
    *[other]purple
}]{$count} {$count ->
    [one]carga
    *[other]cargas
}[/color] restantes.
anomaly-gorilla-charge-infinite = Tiene [color=gold]cargas infinitas[/color]. [italic]Por ahora...[/italic]

anomaly-sync-connected = Anomalía conectada con éxito
anomaly-sync-disconnected = ¡La conexión con la anomalía se ha perdido!
anomaly-sync-no-anomaly = No hay ninguna anomalía en rango.
anomaly-sync-examine-connected = Está [color=darkgreen]conectado[/color] a una anomalía.
anomaly-sync-examine-not-connected = No está [color=darkred]conectado[/color] a ninguna anomalía.
anomaly-sync-connect-verb-text = Conectar anomalía
anomaly-sync-connect-verb-message = Conecta una anomalía cercana a {THE($machine)}.

anomaly-generator-ui-title = Generador de Anomalías
anomaly-generator-fuel-display = Bananium:
anomaly-generator-cooldown = Enfriamiento: [color=gray]{$time}[/color]
anomaly-generator-no-cooldown = Enfriamiento: [color=gray]Completo[/color]
anomaly-generator-yes-fire = Estado: [color=forestgreen]Listo[/color]
anomaly-generator-no-fire = Estado: [color=crimson]No listo[/color]
anomaly-generator-generate = Generar Anomalía
anomaly-generator-charges = {$charges ->
    [one] {$charges} carga
    *[other] {$charges} cargas
}
anomaly-generator-announcement = ¡Se ha generado una anomalía!

anomaly-command-pulse = Pulsa una anomalía objetivo
anomaly-command-supercritical = Hace que una anomalía objetivo entre en estado supercrítico

# Texto de ambientación en el pie
anomaly-generator-flavor-left = La anomalía puede aparecer dentro del operador.
anomaly-generator-flavor-right = v1.1

anomaly-behavior-unknown = [color=red]ERROR. No se puede leer.[/color]

anomaly-behavior-title = análisis de desviación de comportamiento:
anomaly-behavior-point =[color=gold]La anomalía produce el {$mod}% de los puntos[/color]

anomaly-behavior-safe = [color=forestgreen]La anomalía es extremadamente estable. Pulsaciones extremadamente raras.[/color]
anomaly-behavior-slow = [color=forestgreen]La frecuencia de las pulsaciones es mucho menor.[/color]
anomaly-behavior-light = [color=forestgreen]La potencia de pulsación está significativamente reducida.[/color]
anomaly-behavior-balanced = No se detectaron desviaciones de comportamiento.
anomaly-behavior-delayed-force = La frecuencia de las pulsaciones se reduce considerablemente, pero su potencia aumenta.
anomaly-behavior-rapid = La frecuencia de la pulsación es mucho mayor, pero su intensidad está atenuada.
anomaly-behavior-reflect = Se detectó un recubrimiento protector.
anomaly-behavior-nonsensivity = Se detectó una reacción débil a las partículas.
anomaly-behavior-sensivity = Se detectó una reacción amplificada a las partículas.
anomaly-behavior-invisibility = Se ha detectado una distorsión de las ondas de luz.
anomaly-behavior-secret = Interferencia detectada. Algunos datos no se pueden leer
anomaly-behavior-inconstancy = [color=crimson]Se ha detectado impermanencia. Los tipos de partículas pueden cambiar con el tiempo.[/color]
anomaly-behavior-fast = [color=crimson]La frecuencia de pulsación aumenta considerablemente.[/color]
anomaly-behavior-strenght = [color=crimson]La potencia de pulsación aumenta significativamente.[/color]
anomaly-behavior-moving = [color=crimson]Se detectó inestabilidad de coordenadas.[/color]