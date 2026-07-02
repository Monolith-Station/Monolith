## SuspicionGui.xaml.cs

# Se muestra al hacer clic en tu botón de Rol en Suspicion
suspicion-ally-count-display = {$allyCount ->
    *[zero] No tienes aliados
    [one] Tu aliado es {$allyNames}
    [other] Tus aliados son {$allyNames}
}