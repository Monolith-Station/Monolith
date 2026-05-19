## Survivor

roles-antag-survivor-name = Выживший
# It's a Halo reference
roles-antag-survivor-objective = Текущая задача: Выжить
survivor-role-greeting =
    Вы - выживший.
    Ваша главная задача остаться в живых и вернуться на Центком.
    Накопите столько огневой мощи, сколько необходимо для гарантии вашего выживания.
    Никому не доверяйте.
survivor-round-end-dead-count =
    { $deadCount ->
        [one] [color=red]{ $deadCount }[/color] выживший умер.
       *[other] [color=red]{ $deadCount }[/color] выживших умерло.
    }
survivor-round-end-alive-count =
    { $aliveCount ->
        [one] [color=yellow]{ $aliveCount }[/color] выживший остался на станции.
       *[other] [color=yellow]{ $aliveCount }[/color] выживших осталось на станции.
    }
survivor-round-end-alive-on-shuttle-count =
    { $aliveCount ->
        [one] [color=green]{ $aliveCount }[/color] выживший выбрался живым.
       *[other] [color=green]{ $aliveCount }[/color] выживших выбралось живыми.
    }

## Wizard

objective-issuer-swf = [color=turquoise]Федерация Магов[/color]
wizard-title = Маг
wizard-description = На станции появился маг! Никогда не знаешь, что он может сделать.
roles-antag-wizard-name = Маг
roles-antag-wizard-objective = Преподайте им урок, который они никогда не забудут.
wizard-role-greeting =
    ВЫ - МАГ!
    Между Федерация Магов и НаноТрейзен возникла напряженность.
    Поэтому Федерация Магов выбрала вас, чтобы нанести визит на станцию.
    Продемонстрируй им свои способности.
    Что вы будете делать, зависит от вас, но помните, что Федерация Магов хочет, чтобы вы выбрались живыми.
wizard-round-end-name = маг

## TODO: Wizard Apprentice (Coming sometime post-wizard release)

