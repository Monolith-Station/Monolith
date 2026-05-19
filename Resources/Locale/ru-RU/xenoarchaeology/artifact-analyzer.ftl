analysis-console-menu-title = Аналитическая консоль широкого спектра модель 3
analysis-console-server-list-button = Сервер
analysis-console-extract-button = Извлечь очки
analysis-console-info-no-scanner = Анализатор не подключён! Пожалуйста, подключите его с помощью мультитула.
analysis-console-info-no-artifact =
    Артефакт не найден!
    Поместите артефакт на платформу  для получения данных о узлах.
analysis-console-info-ready = Все системы запущены. Сканирование готово.
analysis-console-no-node = Выберите узел для просмотра
analysis-console-info-id = [font="Monospace" size=11]ID:[/font]
analysis-console-info-id-value = [font="Monospace" size=11][color=yellow]{ $id }[/color][/font]
analysis-console-info-class = [font="Monospace" size=11]Класс:[/font]
analysis-console-info-class-value = [font="Monospace" size=11]{ $class }[/font]
analysis-console-info-locked = [font="Monospace" size=11]Статус:[/font]
analysis-console-info-locked-value = [font="Monospace" size=11][color={ $state ->
        [0] red]Заблокирован
        [1] lime]Разблокирован
       *[2] plum]Активен
    }[/color][/font]
analysis-console-info-durability = [font="Monospace" size=11]Прочность:[/font]
analysis-console-info-durability-value = [font="Monospace" size=11][color={ $color }]{ $current }/{ $max }[/color][/font]
analysis-console-info-effect = [font="Monospace" size=11]Эффект:[/font]
analysis-console-info-effect-value = [font="Monospace" size=11][color=gray]{ $state ->
        [true] { $info }
       *[false] Разблокируйте узлы для получения информации
    }[/color][/font]
analysis-console-info-trigger = [font="Monospace" size=11]Стимуляторы:[/font]
analysis-console-info-triggered-value = [font="Monospace" size=11][color=gray]{ $triggers }[/color][/font]
analysis-console-info-scanner = Сканирование...
analysis-console-info-scanner-paused = Пауза.
analysis-console-progress-text =
    { $seconds ->
        [one] T-{ $seconds } секунда
        [few] T-{ $seconds } секунды
       *[other] T-{ $seconds } секунд
    }
analysis-console-extract-value = [font="Monospace" size=11][color=orange]Узел { $id } (+{ $value })[/color][/font]
analysis-console-extract-none = [font="Monospace" size=11][color=orange] У разблокированых узлов не осталось очков для извлечения [/color][/font]
analysis-console-extract-sum = [font="Monospace" size=11][color=orange]Всего изучено: { $value }[/color][/font]
analyzer-artifact-extract-popup = Поверхность артефакта мерцает энергией!
analysis-console-no-points-to-extract = No points to extract.
analysis-console-bias-up = Up
artifact-analyzer-upgrade-duration = Scan Delay
analysis-console-info-triggered-false = ACTIVATED: FALSE
analysis-console-info-depth = DEPTH: { $depth }
analysis-console-info-edges = EDGES: { $edges }
analyzer-artifact-component-upgrade-analysis = analysis duration
analysis-console-scan-button = Scan
analysis-console-print-tooltip-info = Print out the current information about the artifact.
analysis-console-bias-button-info-up = Toggles the bias an artifact has in moving between its nodes. Up heads toward zero depth.
analysis-report-title = Artifact Report: Node { $id }
analysis-console-bias-button-info-down = Toggles the bias an artifact has in moving between its nodes. Down heads toward ever-higher depths.
analysis-console-no-server-connected = Cannot extract. No server connected.
analysis-console-info-triggered-true = ACTIVATED: TRUE
analysis-console-no-artifact-placed = No artifact on scanner.
analysis-console-info-value = UNEXTRACTED_VALUE: { $value }
analysis-console-print-button = Print
analysis-console-bias-down = Down
analysis-console-extract-button-info = Extract points from an artifact based on the newly explored nodes.
analysis-console-print-popup = The console printed out a report.
analysis-console-scan-tooltip-info = Scan artifacts to learn information about their structure.
