mail-recipient-mismatch = El nombre o el trabajo del destinatario no coinciden.
mail-recipient-mismatch-name = El nombre del destinatario no coincide.
mail-invalid-access = El nombre y el trabajo del destinatario coinciden, pero el acceso no es el esperado.
mail-locked = El candado antifraude no se ha retirado. Toca la tarjeta de identificación del destinatario.
mail-desc-far = Un paquete de correo.
mail-desc-close = Un paquete de correo dirigido a {CAPITALIZE($name)}, {$job}. Última ubicación conocida: {$station}.
mail-desc-fragile = Tiene una [color=red]etiqueta roja de frágil[/color].
mail-desc-priority = El [color=yellow]precinto amarillo prioritario[/color] del candado antifraude está activo.
mail-desc-priority-inactive = El [color=#886600]precinto amarillo prioritario[/color] del candado antifraude está inactivo.
mail-unlocked = Sistema antifraude desbloqueado.
mail-unlocked-by-emag = Sistema antifraude *BZZT*.
mail-unlocked-reward = Sistema antifraude desbloqueado. Se han añadido {$bounty} créditos a la cuenta de Frontier.
mail-penalty-lock = CANDADO ANTIFRAUDE ROTO. LA CUENTA BANCARIA DE LA ESTACIÓN HA SIDO PENALIZADA CON {$credits} CRÉDITOS.
mail-penalty-fragile = INTEGRIDAD COMPROMETIDA. LA CUENTA BANCARIA DE LA ESTACIÓN HA SIDO PENALIZADA CON {$credits} CRÉDITOS.
mail-penalty-expired = ENTREGA VENCIDA. LA CUENTA BANCARIA DE LA ESTACIÓN HA SIDO PENALIZADA CON {$credits} CRÉDITOS.
mail-item-name-unaddressed = correo
mail-item-name-addressed = correo ({$recipient})

# Frontier: descripción reescrita, no necesita ser un contenedor.
command-mailto-description = Pone en cola un objeto para ser entregado a un destinatario. Ejemplo de uso: `mailto 1234 5678 false false`. Si la entidad objetivo es un contenedor, su contenido será transferido a un paquete de correo real.
# Frontier: añadir descripción is-large, contenedor<contenidos
command-mailto-help = Uso: {$command} <recipient entityUid> <contents entityUid> [is-fragile: true|false] [is-priority: true|false] [is-large: true|false]
command-mailto-no-mailreceiver = La entidad destinataria no tiene un componente {$requiredComponent}.
command-mailto-no-blankmail = El prototipo {$blankMail} no existe. Algo va muy mal. Contacta con un programador.
command-mailto-bogus-mail = {$blankMail} no tenía {$requiredMailComponent}. Algo va muy mal. Contacta con un programador.
command-mailto-invalid-container = La entidad contenedora objetivo no tiene un contenedor {$requiredContainer}.
command-mailto-unable-to-receive = La entidad destinataria no pudo configurarse para recibir correo. Puede que falte la tarjeta de identificación.
command-mailto-no-teleporter-found = La entidad destinataria no pudo vincularse al teletransportador de correo de ninguna estación. El destinatario puede estar fuera de la estación.
command-mailto-success = ¡Éxito! El paquete de correo ha sido puesto en cola para el próximo teletransporte en {$timeToTeleport} segundos.

# Frontier: completions del comando mailto
command-mailto-completion-recipient = <recipient entityUid>
command-mailto-completion-container = <contents entityUid>
command-mailto-completion-fragile = [is-fragile: true|false]
command-mailto-completion-priority = [is-priority: true|false]
command-mailto-completion-large = [is-large: true|false]
# End Frontier

command-mailnow = Fuerza a todos los teletransportadores de correo a entregar otra ronda de correo lo antes posible. Esto no anulará el límite de correo sin entregar.
command-mailnow-help = Uso: {$command}
command-mailnow-success = ¡Éxito! Todos los teletransportadores de correo entregarán otra ronda de correo pronto.
