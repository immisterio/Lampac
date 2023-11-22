#!/bin/bash

if test -f isdocker; then
  dotnet Lampac.dll
fi

apt-get update
apt-get install -y unzip 
#apt-get install -y ffmpeg 

wget https://github.com/immisterio/Lampac/releases/latest/download/publish.zip
unzip -o publish.zip && rm -f publish.zip && rm -rf ffprobe

touch isdocker

dotnet Lampac.dll
