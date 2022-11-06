# Установка
1. Установить https://learn.microsoft.com/ru-ru/dotnet/core/install/
2. Загрузить и распаковать релиз https://github.com/immisterio/lampac/releases
3. Запустить "dotnet Lampac.dll" (linux) или "Lampac.exe" (windows)

# Альтернативная установка
Установка на linux с помощью скрипта, спасибо @nikk, @Denis

curl -s https://raw.githubusercontent.com/immisterio/lampac/main/install.sh | bash

# Настройки Lampa
1. Парсер Jackett - "IP:9118"
2. Личный прокси  - "http://IP:9118/proxy/" 
3. Плагин онлайн  - планируется в декабре
4. Плагин xxx     - "http://IP:9118/sisi.js"

# Настройки Lampa Lite
1. Плагин онлайн/jackett  - "http://IP:9118/lite.js" 
2. Плагин xxx     - "http://IP:9118/sisi.js"

# Общие настройки
1. Открыть настройки, раздел "Остальное"
2. В "Основной источник" выбрать "CUB"

# Источники 
* Public online  - Videocdn, Rezka, Kinobase, Collaps, Filmix, Kinokrad, Kinotochka, Kinoprofi, LostfilmHD, VideoAPI (ENG), Ashdi (UKR), Eneyida (UKR)
* Private online - HDVB (FREE), IframeVideo (FREE), Bazon (PAY), Alloha (PAY), Kodik (PAY), Seasonvar (PAY)
* Public Trackers  - kinozal.tv, nnmclub.to, rutor.info, megapeer.vip, torrent.by, bitru.org, anilibria.tv
* Private Trackers - toloka.to, rutracker.net, underver.se, selezen.net, animelayer.ru
* Клубничка bongacams.com, chaturbate.com, ebalovo.pro, eporner.com, hqporner.com, porntrex.com, spankbang.com, xhamster.com, xnxx.com, xvideos.com

# Плагин Tracks.js
Заменяет название аудиодорожек и субтитров в плеере, работает только в торрентах

Автор: @aabytt

1. Добавить плагин "http://IP:9118/tracks.js" 
2. В init.conf заменить значение "ffprobe" на один из вариантов "win.exe", "linux", "arm"

# Плагин TmdbProxy.js
Проксирование постеров для сайта TMDB

1. Добавить плагин "http://IP:9118/tmdbproxy.js" 
2. В настройках TMDB включить проксирование

# Параметры init.conf
* xdb - Выводит платные источники с sisi.am
* cachetype - Место хранения кеша "file", "mem" 
* emptycache - Сохраняет пустой результат как валидный кеш (рекомендуется включать при публичном использование)
* timeoutSeconds - Максимальное время ожидания ответа от трекера
* fileCacheInactiveDay - Время хранения резервного кеша на диске
* multiaccess - Настройка кеша в онлайн с учетом многопользовательского доступа
* useproxy - Парсит источник через прокси указанные в "proxy"
* streamproxy - Перенаправляет видео через "http://IP:9118/proxy/{uri}" 
* disableserverproxy - Запрещает запросы через "http://IP:9118/(proxy|proxyimg)/"
* localip - Заменить на "false" если скрипт установлен за пределами внутренней сети

# Настройки при совместном использовании
* timeoutSeconds - 10
* cachetype - file
* htmlCacheToMinutes - 20
* magnetCacheToMinutes - 40
* emptycache - true
* multiaccess - true

# Доступ к доменам .onion
1. Запустить tor на порту 9050
2. В init.conf указать onion домен в host

# Media Station X
1. Settings -> Start Parameter -> Setup
2. Enter current ip address and port "IP:9118"

Убрать/Добавить адреса можно в msx.json
