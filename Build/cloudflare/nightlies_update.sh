#!/usr/bin/env bash
cd /home/lampac

systemctl stop lampac

rm -f update.zip
wget https://bwa.pages.dev/lpc/update.zip
unzip -o update.zip
rm -f update.zip

systemctl start lampac
