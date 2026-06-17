# Транскодинг видео и аудио

Включите модуль в init.conf/yaml: 

```json
"gst": {
  "enable": true
}
```

Добавьте адрес плагина на устройстве, где требуется транскодинг:

```text
http://IP:9118/gst.js
```

## Linux (Debian/Ubuntu)

Требуется **GStreamer 1.28 или новее**.

```bash
apt-get update

apt-get install -y --no-install-recommends \
    libgstreamer1.0-0 \
    libgstreamer-plugins-base1.0-0 \
    gstreamer1.0-plugins-base \
    gstreamer1.0-plugins-good \
    gstreamer1.0-plugins-bad \
    gstreamer1.0-libav \
    gstreamer1.0-plugins-base-apps \
    gstreamer1.0-tools \
    ca-certificates
```

Проверка версии:

```bash
gst-inspect-1.0 --version
gst-discoverer-1.0 --version
```

## Windows

Скачайте и установите:

https://gstreamer.freedesktop.org/data/pkg/windows/1.28.3/mingw/gstreamer-1.0-mingw-x86_64-1.28.3.exe

Во время установки выберите:

```text
Install mode: Only runtime
```

Путь по умолчанию:

```text
C:\Program Files\gstreamer\1.0\mingw_x86_64
```

Если mingw установлен в другое место, укажите каталог в настройке `PATH` модуля.
