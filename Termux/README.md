<b>Supports:</b> smartphone, tablet, tv-box aosp
<br><b>Note:</b> Android TV (ATV) not support! 

# Download Termux
Скачайте и установите Termux с <a href="https://play.google.com/store/apps/details?id=com.termux&hl=ru" target="_blank">Google Play</a> or <a href="https://github.com/termux/termux-app/releases" target="_blank">GitHub official</a>  or  <a href="https://f-droid.org/ru/packages/com.termux/" target="_blank">F-Droid</a>

# Установка Lampac
Запустите Termux и выполните команду
```
curl -L -k -s https://lampac.sh/termux | bash
```
Спасибо @bbk14

# Плагины
http://127.0.0.1:9118 - лампа без рекламы<br>
http://127.0.0.1:9118/online.js - онлайн <br>
http://127.0.0.1:9118/sisi.js - 18+

# Источники
https://github.com/immisterio/Lampac?tab=readme-ov-file#%D0%B8%D1%81%D1%82%D0%BE%D1%87%D0%BD%D0%B8%D0%BA%D0%B8
* Zetflix и ENG сайты не поддерживается в Termux 

# Команды в Termux
```
bash stop.sh
bash start.sh
bash restart.sh
bash update.sh # обновить версию lampac
```

# Важно
* Изменить настройки и привязать pro аккаунты можно через http://127.0.0.1:9118/admin
* Из за низкой производительности termux, включать chrome/firefox/torrserver/proxy/jacred/dlna не рекомендуется от слома совсем
* На балансерах с отключённым streamproxy нужно использовать исключительно внешний плеер Vimu, MPV, MX player с поддержкой headers, иначе видео на некоторых балансерах будет выдавать ошибку воспроизведения
