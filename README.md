# Установка
1. Установить https://learn.microsoft.com/ru-ru/dotnet/core/install/
2. Загрузить и распаковать релиз https://github.com/immisterio/lampac/releases
3. Запустить "dotnet Lampac.dll" (linux) или "Lampac.exe" (windows)

# Альтернативная установка
Установка на linux с помощью скрипта, спасибо @nikk, @Denis

curl -s https://raw.githubusercontent.com/immisterio/lampac/main/install.sh | bash

# Настройки Lampa
1. Плагин онлайн  - "http://IP:9118/online.js"
2. Плагин xxx     - "http://IP:9118/sisi.js"
3. Плагин DLNA    - "http://IP:9118/dlna.js"
4. Плагин Tracks  - "http://IP:9118/tracks.js"
5. Парсер Jackett - "IP:9118"

# Настройки Lampa Lite
1. Плагин онлайн/jackett  - "http://IP:9118/lite.js" 
2. Плагин xxx     - "http://IP:9118/sisi.js"

# Общие настройки
1. Открыть настройки, раздел "Остальное"
2. В "Основной источник" выбрать "CUB"

# Источники 
* Online - Filmix, Kinobase, HDRezka (Voidboost), VideoCDN, VideoDB, Collaps, HDVB, Zetflix (VideoDB), Kodik, Ashdi (UKR), Eneyida (UKR), Kinokrad, Kinotochka, Kinoprofi, LostfilmHD, IframeVideo, CDNmovies, Redheadsound, Anilibria, AniMedia, AnimeGo, VideoAPI (ENG), Bazon (PAY), Alloha (PAY), Seasonvar (PAY), KinoPub (PAY)
* Trackers  - kinozal, nnmclub, rutor, megapeer, torrent, bitru, anilibria, toloka, rutracker, underver, selezen, animelayer
* Клубничка pornhub, bongacams, chaturbate, ebalovo, eporner, hqporner, porntrex, spankbang, xhamster, xnxx, xvideos

# Привязка PRO аккаунтов
* Filmix - "http://IP:9118/lite/filmixpro" 
* KinoPub - "http://IP:9118/lite/kinopubpro" 

# Плагин DLNA.js
* Просмотр медиа файлов с папки dlna
* Возможность удалять просмотренные папки/файлы
* Загрузка торрентов в папку dlna

Зажмите кнопку "OK" на выбранном торренте/папке/файле для вызова списка действий

# Плагин Tracks.js
Заменяет название аудиодорожек и субтитров в плеере

Автор: @aabytt

1. Добавить плагин "http://IP:9118/tracks.js" 
2. В init.conf заменить значение "ffprobe" на один из вариантов "win.exe", "linux"

# Плагин TmdbProxy.js
Проксирование постеров для сайта TMDB

1. Добавить плагин "http://IP:9118/tmdbproxy.js" 
2. В настройках TMDB включить проксирование

# Параметры init.conf
* xdb - Выводит платные источники с sisi.am
* cachetype - Место хранения кеша "file", "mem" 
* emptycache - Сохраняет пустой результат как валидный кеш (рекомендуется включать при публичном использование)
* priority - Отдавать торрент в виде magnet ссылки, либо torrent файл (magnet|torrent)
* timeoutSeconds - Максимальное время ожидания ответа от трекера
* fileCacheInactiveDay - Время хранения резервного кеша на диске
* multiaccess - Настройка кеша в онлайн с учетом многопользовательского доступа
* useproxy - Парсит источник через прокси указанные в "proxy"
* streamproxy - Перенаправляет видео через "http://IP:9118/proxy/{uri}" 
* disableserverproxy - Запрещает запросы через "http://IP:9118/(proxy|proxyimg)/"
* localip - Заменить на "false" если скрипт установлен за пределами внутренней сети
* proxytoproxyimg - Использовать прокси при получении картинки в "http://IP:9118/proxyimg/"
* SisiHeightPicture - Уменьшение размера картинки в xxx по высоте до 200px
* findkp - Каталог для поиск kinopoisk_id (alloha|tabus|vsdn)

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
