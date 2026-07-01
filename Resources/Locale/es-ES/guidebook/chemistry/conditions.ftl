reagent-effect-condition-guidebook-total-damage =
    { $max ->
        [2147483648] tiene al menos {NATURALFIXED($min, 2)} de daño total
        *[other] { $min ->
                    [0] tiene como mucho {NATURALFIXED($max, 2)} de daño total
                    *[other] tiene entre {NATURALFIXED($min, 2)} y {NATURALFIXED($max, 2)} de daño total
                 }
    }

reagent-effect-condition-guidebook-total-hunger =
    { $max ->
        [2147483648] el objetivo tiene al menos {NATURALFIXED($min, 2)} de hambre total
        *[other] { $min ->
                    [0] el objetivo tiene como mucho {NATURALFIXED($max, 2)} de hambre total
                    *[other] el objetivo tiene entre {NATURALFIXED($min, 2)} y {NATURALFIXED($max, 2)} de hambre total
                 }
    }

reagent-effect-condition-guidebook-reagent-threshold =
    { $max ->
        [2147483648] hay al menos {NATURALFIXED($min, 2)}u de {$reagent}
        *[other] { $min ->
                    [0] hay como mucho {NATURALFIXED($max, 2)}u de {$reagent}
                    *[other] hay entre {NATURALFIXED($min, 2)}u y {NATURALFIXED($max, 2)}u de {$reagent}
                 }
    }

reagent-effect-condition-guidebook-mob-state-condition =
    el personaje está { $state }

reagent-effect-condition-guidebook-job-condition =
    el trabajo del objetivo es { $job }

reagent-effect-condition-guidebook-solution-temperature =
    la temperatura de la solución es { $max ->
            [2147483648] al menos {NATURALFIXED($min, 2)}k
            *[other] { $min ->
                        [0] como mucho {NATURALFIXED($max, 2)}k
                        *[other] entre {NATURALFIXED($min, 2)}k y {NATURALFIXED($max, 2)}k
                     }
    }

reagent-effect-condition-guidebook-body-temperature =
    la temperatura corporal es { $max ->
            [2147483648] al menos {NATURALFIXED($min, 2)}k
            *[other] { $min ->
                        [0] como mucho {NATURALFIXED($max, 2)}k
                        *[other] entre {NATURALFIXED($min, 2)}k y {NATURALFIXED($max, 2)}k
                     }
    }

reagent-effect-condition-guidebook-organ-type =
    el órgano metabolizante { $shouldhave ->
                                [true] es
                                *[false] no es
                           } {INDEFINITE($name)} {$name} órgano

reagent-effect-condition-guidebook-has-tag =
    el objetivo { $invert ->
                 [true] no tiene
                 *[false] tiene
                } la etiqueta {$tag}

reagent-effect-condition-guidebook-blood-reagent-threshold =
    { $max ->
        [2147483648] hay al menos {NATURALFIXED($min, 2)}u de {$reagent}
        *[other] { $min ->
                    [0] hay como mucho {NATURALFIXED($max, 2)}u de {$reagent}
                    *[other] hay entre {NATURALFIXED($min, 2)}u y {NATURALFIXED($max, 2)}u de {$reagent}
                 }
    }

reagent-effect-condition-guidebook-this-reagent = este reactivo
