#!/bin/bash

if test -f isdocker; then
  return
fi

apt-get update
apt-get install -y unzip ffmpeg nano
apt-get install -y libnss3-dev libgdk-pixbuf2.0-dev libgtk-3-dev libxss-dev

wget https://github.com/immisterio/Lampac/releases/latest/download/publish.zip
unzip -o publish.zip && rm -f publish.zip

touch isdocker

dotnet Lampac.dll
