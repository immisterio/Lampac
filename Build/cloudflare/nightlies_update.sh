#!/usr/bin/env bash

VERSION=$1
CUSTOM_DIR=$2

if [ -n "$CUSTOM_DIR" ]; then
    WORK_DIR="$CUSTOM_DIR"
else
    WORK_DIR="/home/lampac"
fi

if [ -n "$VERSION" ]; then
    UPDATEURI="https://${VERSION}.bwa.pages.dev/lpc/update.zip"
else
    UPDATEURI="https://bwa.pages.dev/lpc/update.zip"
fi

cd "$WORK_DIR" || { echo "Failed to change directory to $WORK_DIR. Exiting."; exit 1; }

rm -f update.zip

echo -e "Download $UPDATEURI \n"

if ! curl -L -k -o update.zip "$UPDATEURI"; then
    echo -e "\nFailed to download update.zip. Exiting."
    exit 1
fi
if ! unzip -t update.zip; then
    echo -e "\nFailed to test update.zip. Exiting."
    exit 1
fi

systemctl stop lampac
unzip -o update.zip
rm -f update.zip
systemctl start lampac

echo -e "\n\nUpdate completed successfully in directory: $WORK_DIR"