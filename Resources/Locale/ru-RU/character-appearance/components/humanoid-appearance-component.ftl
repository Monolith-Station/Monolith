humanoid-appearance-component-unknown-species = гуманоид
humanoid-appearance-component-examine = { CAPITALIZE(SUBJECT($user)) } { $species } { $age }.
humanoid-appearance-component-mindset-examine =
    Мировозрение: { $mindset ->
        [orderlykind] Порядочно-добрый
       *[ordered] Порядочно-нейтральный
        [orderlyangry] Порядочно-злой
        [good] Нейтрально-добрый
        [trulyneutral] Нейтральный
        [evil] Нейтрально-злой
        [chaoticallykind] Хаотично-добрый
        [chaotic] Хаотично-нейтральный
        [chaoticallyangry] Хаотично-злой
    }
