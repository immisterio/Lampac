# Установка
1. Установить ".NET Core 6" https://learn.microsoft.com/ru-ru/dotnet/core/install/
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
* Filmix, Kinobase, HDRezka (Voidboost), VideoCDN, VideoDB, Collaps, HDVB, Zetflix (VideoDB), Kodik, Ashdi (UKR), Eneyida (UKR), Kinokrad, Kinotochka, Kinoprofi, LostfilmHD, IframeVideo, CDNmovies, Anilibria, AniMedia, AnimeGo, Animevost, Animebesst, Redheadsound, VideoAPI (ENG), Bazon, Alloha, Seasonvar, KinoPub
* Kinozal, Nnmclub, Rutor, Megapeer, Torrentby, Bitru, Anilibria, Toloka (UKR), Rutracker, Underver, Selezen, Animelayer, Anifilm
* PornHub, Bongacams, Chaturbate, Ebalovo, Eporner, HQporner, Porntrex, Spankbang, Xhamster, Xnxx, Xvideos

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

Для отключения оставьте значение "ffprobe" пустым

# Плагин TmdbProxy.js
Проксирование постеров для сайта TMDB

1. Добавить плагин "http://IP:9118/tmdbproxy.js" 
2. В настройках TMDB включить проксирование

# Доступ к доменам .onion
1. Запустить tor на порту 9050
2. В init.conf указать .onion домен в host

# Media Station X
1. Settings -> Start Parameter -> Setup
2. Enter current ip address and port "IP:9118"

Убрать/Добавить адреса можно в msx.json

# Виджеты
1. Для Samsung "IP:9118/samsung.wgt"

# Параметры init.conf
* xdb - Выводит платные источники с sisi.am
* cachetype - Место хранения кеша "file", "mem" 
* emptycache - Сохраняет пустой результат как валидный кеш (рекомендуется включать при публичном использование)
* priority - Отдавать торрент в виде magnet ссылки, либо torrent файл (magnet|torrent)
* timeoutSeconds - Максимальное время ожидания ответа от трекера
* litejac - Включить Jackett в Lampa Lite
* search_lang - Язык поиска на трекерах "title_original - en", "title - ru", "query - настройки lampa" 
* fileCacheInactiveDay - Время хранения резервного кеша на диске
* checkOnlineSearch - Делать предварительный поиск скрывая балансеры без ответа
* multiaccess - Настройка кеша в онлайн с учетом многопользовательского доступа
* accsdb - Доступ к API через авторизацию (для jackett используется apikey)
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
* torrentCacheToMinutes - 40
* fileCacheInactiveDay - html 3, img 1, torrent 5
* emptycache - true
* multiaccess - true

# Пример init.conf
* Список всех параметров, а так же значения по умолчанию смотреть в example.conf 
* В init.conf нужно указывать только те параметры, которые хотите изменить

```
{
  "listenport": 9120, // изменили порт
  "jac": {
    "cachetype": "mem", // изменили место хранения кеша
    "apikey": "1"       // запретили доступ без ключа авторизации
  },
  "dlna": {
    "downloadSpeed": 25000000 // ограничили скорость загрузки до 200 Mbit/s
  },
  "sisi": {
    "xdb": true // вывели доп. источники с sisi.am
  },
  "Rutracker": {
    "enable": true, // включили rutracker указав данные для авторизации 
    "login": {
      "u": "megachel",
      "p": "iegoher"
    }
  },
  "NNMClub": { // изменили домен на адрес из сети tor 
    "host": "http://nnmclub2vvjqzjne6q4rrozkkkdmlvnrcsyes2bbkm7e5ut2aproy4id.onion"
  },
  "Rezka": {
    "streamproxy": true // отправили видеопоток через "http://IP:9118/proxy/{uri}" 
  },
  "Filmix": {
    "token": "protoken" // добавили токен от PRO аккаунта
  },
  "PornHub": {
    "enable": false // отключили PornHub
  },
  "globalproxy": [
    {
      "pattern": "\\.onion",  // запросы на домены .onion отправить через прокси
      "list": [
        "socks5://127.0.0.1:9050" // прокси сервер tor
      ]
    }
  ],
  "overrideResponse": [ // Заменили ответ на данные из файла myfile.json
    {
      "pattern": "/msx/start.json",
      "action": "file",
      "type": "application/json; charset=utf-8",
      "val": "myfile.json"
    }
  ]
}
```
