#!/usr/bin/bash
DEST="/home/lampac"

systemctl stop lampac

cd $DEST
wget https://github.com/immisterio/Lampac/releases/latest/download/publish.zip
unzip -o publish.zip
rm -f publish.zip

systemctl start lampac
