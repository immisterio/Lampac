# FlixCDN

Онлайн-источник **FlixCDN**: прямой плеер **`https://tarantino.factorios.live`**, **`streamproxy: true`**. По умолчанию источник выключен: **`enable = false`**.

## Интерфейс

**`IModuleLoaded`**, **`IModuleOnline`**.

## Условие (`Invoke`)

Добавляется пункт, если **`args.kinopoisk_id > 0`** и **`!args.isanime`** (не аниме-запрос).

## Конфигурация

Секция в `init.conf`: **`FlixCDN`** (`OnlinesSettings`).

По умолчанию: **`displayindex = 525`**, **`httpversion = 1`**, **`rch_access`**, **`stream_access`**, **`headers_stream`** под домен плеера.

## Проверка доступа

Ссылки получает официальный плеер через отдельный headed Chromium, который
запускается только для FlixCDN. Глобальный режим Chromium Lampac не изменяется.
Для каждого запроса используется чистый контекст с реальным User-Agent браузера
и выбранным прокси. Готовые ссылки сохраняются в существующем кэше; видеопоток
передаётся обычным stream-proxy. Одновременно разрешены две браузерные проверки,
а Chromium закрывается после пяти минут простоя.

В глобальной секции `chromium` должны быть включены `enable` и корректный
`executablePath`; значение `Headless` на FlixCDN не влияет. На Linux/Docker
необходим установленный `/usr/bin/Xvfb` (он входит в официальный Docker-образ).
Модуль запускает его на свободном display и закрывает вместе со своим браузером.
При `FlixCDN.priorityBrowser = "http"` браузерная проверка отключена.

## Подпись качества

**`OnlineApiQuality`**: при **`e.balanser == "flixcdn"`** → **` ~ 1080p`**.

## HTTP

| Маршрут | Назначение |
|---------|------------|
| **`lite/flixcdn`** | Основная выдача. |
| **`lite/flixcdn/stream`** | Поток (см. **`Controller.cs`**). |

## Файлы

**`ModInit.cs`**, **`Controller.cs`**.
