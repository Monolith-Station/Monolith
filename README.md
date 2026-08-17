<p align="center"> <img alt="Frontier Station 14" width="880" height="300" src="https://raw.githubusercontent.com/Monolith-Station/Monolith/89d435f0d2c54c4b0e6c3b1bf4493c9c908a6ac7/Resources/Textures/_Mono/Logo/logo.png?raw=true" /></p>

**Corvax Forge Monolith** - это русскоязычное ответвление **Monolith**, форка [Monolith](https://github.com/Monolith-Station), которое работает на движке [Robust Toolbox](https://github.com/space-wizards/RobustToolbox), написанном на C#.

В этой сборке представлены собственные наработки, адаптации и контент, созданный русскоязычным комьюнити.
Если вы хотите разместить сервер или разрабатывать контент для **Corvax Forge Monolith**, используйте этот репозиторий. Он включает **RobustToolbox** и контент-пак для создания новых дополнений.

## Ссылки

<div class="header" align="center">

[Discord](https://discord.gg/7wDwSPde58) | [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/) | [Boosty](https://boosty.to/corvaxforge) | [Вики](https://station14.ru/wiki/%D0%9F%D0%BE%D1%80%D1%82%D0%B0%D0%BB:Frontier)


</div> 

## Сборка

Обратитесь к [руководству Space Wizards](https://docs.spacestation14.com/en/general-development/setup/setting-up-a-development-environment.html) по настройке среды разработки для получения общей информации, но имейте в виду, что Corvax Forge Monolith — это не то же самое, и многие вещи могут не применяться.
Мы предоставляем несколько скриптов, показанных ниже, чтобы упростить работу.

### Необходимые программы

- Git
- .NET SDK 10.0.X

### Windows

```
1. Клонируйте этот репозиторий
2. Запустите `Scripts/bat/updateEngine.bat` в терминале или в проводнике, чтобы загрузить движок
3. Запустите `Scripts/bat/buildAllDebug.bat` после внесения любых изменений в исходный код
4. Запустите `Scripts/bat/runQuickAll.bat`, чтобы запустить клиент и сервер
5. Подключитесь к localhost в клиенте и играйте
```

### Linux
```
1. Клонируйте этот репозиторий
2. Запустите `Scripts/sh/updateEngine.sh` в терминале, чтобы загрузить движок
3. Запустите `Scripts/sh/buildAllDebug.sh` после внесения любых изменений в исходный код
4. Запустите `Scripts/sh/runQuickAll.sh`, чтобы запустить клиент и сервер
5. Подключитесь к localhost в клиенте и играйте
```
## Это всё локальный сервер для небольших запусков. Никакой коммерции.
