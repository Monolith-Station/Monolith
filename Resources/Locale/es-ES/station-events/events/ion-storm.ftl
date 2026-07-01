station-event-ion-storm-start-announcement = Tormenta iónica detectada cerca de la estación. Por favor, comprueba todos los equipos controlados por IA en busca de errores.

ion-storm-law-scrambled-number = [font="Monospace"][scramble rate=250 length={$length} chars="@@###$$&%!01"/][/font]

ion-storm-you = TÚ
# Frontier: la estación < el sector
ion-storm-the-station = EL SECTOR
ion-storm-the-crew = LA TRIPULACIÓN
ion-storm-the-job = EL {$job}
ion-storm-clowns = LOS PAYASOS
# Frontier: jefes de personal < mando del sector
ion-storm-heads = MANDO DEL SECTOR
ion-storm-crew = TRIPULACIÓN
ion-storm-people = PERSONAS

ion-storm-adjective-things = {$adjective} COSAS
ion-storm-x-and-y = {$x} Y {$y}

# joined es abreviatura de {$number} {$adjective}
# subjects generalmente pueden ser amenazas, trabajos u objetos
# thing está especificado arriba
# Frontier: "en la estación" < "en el sector"
ion-storm-law-on-station = HAY {$joined} {$subjects} EN EL SECTOR
ion-storm-law-no-shuttle = EL FIN DEL TURNO NO PUEDE DECLARARSE POR CULPA DE {$joined} {$subjects} EN FRONTIER
ion-storm-law-crew-are = LOS {$who} SON AHORA {$joined} {$subjects}

ion-storm-law-subjects-harmful = {$adjective} {$subjects} SON PERJUDICIALES PARA LA TRIPULACIÓN
ion-storm-law-must-harmful = QUIENES {$must} SON PERJUDICIALES PARA LA TRIPULACIÓN
# thing es un concepto o acción
ion-storm-law-thing-harmful = {$thing} ES PERJUDICIAL PARA LA TRIPULACIÓN
ion-storm-law-job-harmful = {$adjective} {$job} SON PERJUDICIALES PARA LA TRIPULACIÓN
# thing es objetos o concepto, el adjetivo aplica en ambos casos
# esto significa que puedes obtener una ley como "NO TENER COMUNISMO-ROBADOR-DE-NAVIDAD ES PERJUDICIAL PARA LA TRIPULACIÓN" :)
ion-storm-law-having-harmful = TENER {$adjective} {$thing} ES PERJUDICIAL PARA LA TRIPULACIÓN
ion-storm-law-not-having-harmful = NO TENER {$adjective} {$thing} ES PERJUDICIAL PARA LA TRIPULACIÓN

# thing es un concepto o requisito
ion-storm-law-requires = {$who} {$plural ->
    [true] REQUIEREN
    *[false] REQUIERE
} {$thing}
ion-storm-law-requires-subjects = {$who} {$plural ->
    [true] REQUIEREN
    *[false] REQUIERE
} {$joined} {$subjects}

ion-storm-law-allergic = {$who} {$plural ->
    [true] SON
    *[false] ES
} {$severity} ALÉRGICO A {$allergy}
ion-storm-law-allergic-subjects = {$who} {$plural ->
    [true] SON
    *[false] ES
} {$severity} ALÉRGICO A {$adjective} {$subjects}

ion-storm-law-feeling = {$who} {$feeling} {$concept}
ion-storm-law-feeling-subjects = {$who} {$feeling} {$joined} {$subjects}

ion-storm-law-you-are = AHORA ERES {$concept}
ion-storm-law-you-are-subjects = AHORA ERES {$joined} {$subjects}
ion-storm-law-you-must-always = SIEMPRE DEBES {$must}
ion-storm-law-you-must-never = NUNCA DEBES {$must}

ion-storm-law-eat = EL {$who} DEBE COMER {$adjective} {$food} PARA SOBREVIVIR
ion-storm-law-drink = EL {$who} DEBE BEBER {$adjective} {$drink} PARA SOBREVIVIR

ion-storm-law-change-job = LOS {$who} SON AHORA {$adjective} {$change}
ion-storm-law-highest-rank = LOS {$who} SON AHORA LOS MIEMBROS DE TRIPULACIÓN DE MAYOR RANGO
ion-storm-law-lowest-rank = LOS {$who} SON AHORA LOS MIEMBROS DE TRIPULACIÓN DE MENOR RANGO

ion-storm-law-crew-must = EL {$who} DEBE {$must}
ion-storm-law-crew-must-go = EL {$who} DEBE IR A {$area}

ion-storm-part = {$part ->
    [true] PARTE
    *[false] NO PARTE
}
# debido a la redacción, esto significaría una ley como
# SOLO LOS HUMANOS NO SON PARTE DE LA TRIPULACIÓN
# haría que los nukies/syndies/lo que sea no humanos sean tripulación :)
ion-storm-law-crew-only-1 = SOLO LOS {$who} SON {$part} DE LA TRIPULACIÓN
ion-storm-law-crew-only-2 = SOLO LOS {$who} Y {$other} SON {$part} DE LA TRIPULACIÓN
ion-storm-law-crew-only-subjects = SOLO {$adjective} {$subjects} SON {$part} DE LA TRIPULACIÓN
ion-storm-law-crew-only-species = TODOS LOS {$species} SON {$part} DE LA TRIPULACIÓN
ion-storm-law-crew-must-do = SOLO QUIENES {$must} SON {$part} DE LA TRIPULACIÓN
ion-storm-law-crew-must-have = SOLO QUIENES TIENEN {$adjective} {$objects} SON {$part} DE LA TRIPULACIÓN
ion-storm-law-crew-must-eat = SOLO QUIENES COMEN {$adjective} {$food} SON {$part} DE LA TRIPULACIÓN

ion-storm-law-harm = DEBES HACER RESBALAR A {$who} Y NO PERMITIRLES, POR INACCIÓN, ESCAPAR DEL RESBALÓN
ion-storm-law-protect = NUNCA DEBES DAÑAR A {$who} Y NO PERMITIRLES, POR INACCIÓN, SUFRIR DAÑO

# implementar otras variantes es molesto así que solo se usa esta
# EL COMUNISMO ESTÁ MATANDO PAYASOS
ion-storm-law-concept-verb = {$concept} ESTÁ {$verb} {$subjects}

# omitiendo el renombramiento ya que es molesto para los jugadores hacer seguimiento
