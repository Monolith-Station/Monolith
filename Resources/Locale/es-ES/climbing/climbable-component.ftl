
### Interfaz

# Nombre del verbo para trepar
comp-climbable-verb-climb = Saltar

### Mensajes de interacción

# Se muestra cuando tu personaje trepa sobre $climbable
comp-climbable-user-climbs = ¡Saltas sobre { THE($climbable) }!

# Se muestra a otros cuando $user trepa sobre $climbable
comp-climbable-user-climbs-other  = ¡{ CAPITALIZE(THE($user)) } salta sobre { THE($climbable) }!

# Se muestra cuando tu personaje fuerza a alguien a trepar sobre $climbable
comp-climbable-user-climbs-force = ¡Fuerzas a { THE($moved-user) } sobre { THE($climbable) }!

# Se muestra a otros cuando alguien fuerza a $moved-user a trepar sobre $climbable
comp-climbable-user-climbs-force-other = { CAPITALIZE(THE($user)) } fuerza a { THE($moved-user) } sobre { THE($climbable) }!

# Se muestra cuando tu personaje está lejos del objeto escalable
comp-climbable-cant-reach = ¡No puedes llegar ahí!

# Se muestra cuando tu personaje no puede interactuar con el objeto escalable por alguna razón
comp-climbable-cant-interact = ¡No puedes hacer eso!

# Se muestra cuando tu personaje no puede trepar por sus propias acciones
comp-climbable-cant-climb = ¡Eres incapaz de trepar!

# Se muestra cuando tu personaje intenta forzar a alguien que no puede trepar sobre un objeto escalable
comp-climbable-target-cant-climb = ¡{ CAPITALIZE(THE($moved-user)) } no puede ir ahí!
