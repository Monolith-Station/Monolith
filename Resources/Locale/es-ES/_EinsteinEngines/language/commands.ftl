command-list-langs-desc = Lista los idiomas que tu entidad actual puede hablar en este momento.
command-list-langs-help = Uso: {$command}

command-saylang-desc = Envía un mensaje en un idioma específico. Para elegir un idioma, puedes usar el nombre del idioma o su posición en la lista de idiomas.
command-saylang-help = Uso: {$command} <id de idioma> <mensaje>. Ejemplo: {$command} TauCetiBasic "Hello World!". Ejemplo: {$command} 1 "Hello World!"

command-language-select-desc = Selecciona el idioma que habla actualmente tu entidad. Puedes usar el nombre del idioma o su posición en la lista de idiomas.
command-language-select-help = Uso: {$command} <id de idioma>. Ejemplo: {$command} 1. Ejemplo: {$command} TauCetiBasic

command-language-spoken = Hablados:
command-language-understood = Entendidos:
command-language-current-entry = {$id}. {$language} - {$name} (actual)
command-language-entry = {$id}. {$language} - {$name}

command-language-invalid-number = El número de idioma debe estar entre 0 y {$total}. Alternativamente, usa el nombre del idioma.
command-language-invalid-language = El idioma {$id} no existe o no puedes hablarlo.

# Toolshed

command-description-language-add = Añade un nuevo idioma a la entidad conectada. Los dos últimos argumentos indican si debe ser hablado/entendido. Ejemplo: 'self language:add "Canilunzt" true true'
command-description-language-rm = Elimina un idioma de la entidad conectada. Funciona de forma similar a language:add. Ejemplo: 'self language:rm "TauCetiBasic" true true'.
command-description-language-lsspoken = Lista todos los idiomas que la entidad puede hablar. Ejemplo: 'self language:lsspoken'
command-description-language-lsunderstood = Lista todos los idiomas que la entidad puede entender. Ejemplo: 'self language:lssunderstood'

command-description-translator-addlang = Añade un nuevo idioma objetivo a la entidad traductora conectada. Consulta language:add para más detalles.
command-description-translator-rmlang = Elimina un idioma objetivo de la entidad traductora conectada. Consulta language:rm para más detalles.
command-description-translator-addrequired = Añade un nuevo idioma requerido a la entidad traductora conectada. Ejemplo: 'ent 1234 translator:addrequired "TauCetiBasic"'
command-description-translator-rmrequired = Elimina un idioma requerido de la entidad traductora conectada. Ejemplo: 'ent 1234 translator:rmrequired "TauCetiBasic"'
command-description-translator-lsspoken = Lista todos los idiomas hablados de la entidad traductora conectada. Ejemplo: 'ent 1234 translator:lsspoken'
command-description-translator-lsunderstood = Lista todos los idiomas entendidos de la entidad traductora conectada. Ejemplo: 'ent 1234 translator:lssunderstood'
command-description-translator-lsrequired = Lista todos los idiomas requeridos de la entidad traductora conectada. Ejemplo: 'ent 1234 translator:lsrequired'

command-language-error-this-will-not-work = Esto no funcionará.
command-language-error-not-a-translator = La entidad {$entity} no es un traductor.
