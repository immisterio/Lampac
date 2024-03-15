#!/bin/bash

if test -f isdocker; then
  return
fi

mkdir -p /home -m 777
cd home

apt-get update
apt-get install -y wget unzip ffmpeg
apt-get install -y chromium-browser
#apt-get install -y libnss3-dev libgdk-pixbuf2.0-dev libgtk-3-dev libxss-dev

wget https://github.com/immisterio/Lampac/releases/latest/download/publish.zip
unzip -o publish.zip && rm -f publish.zip

touch isdocker

echo '{"puppeteer": {"executablePath": "/usr/bin/chromium-browser"}, "isarm": true, "mikrotik":true, "serverproxy": {"verifyip":false}}' > /home/init.conf

dotnet Lampac.dll
