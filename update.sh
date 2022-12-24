#!/usr/bin/bash
DEST="/home/lampac"

systemctl stop lampac

cd $DEST
mv msx.json msx.json.back
wget https://github.com/immisterio/Lampac/releases/latest/download/publish.zip
unzip -o publish.zip
rm -f publish.zip msx.json
mv msx.json.back msx.json

systemctl start lampac