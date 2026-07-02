# Capibara ESP — fork-owned locale keys. Safe to edit (never in upstream).
capibara-loc-smoke = Prueba de localización de Capibara

# Regression seed for CapibaraCultureTest: MANY/MAKEPLURAL must be registered for
# the es-ES culture (they used to be en-US-only → "Unknown function: MANY()").
capibara-loc-many = {MANY("segundo", $count)}

# Fix for a key upstream references in ChannelFilterPopup.xaml but never defines
# in any locale (would render as the raw key id).
hud-chatbox-highlights-tooltip = Aplica las palabras a resaltar en el chat. Una palabra o frase por línea.
