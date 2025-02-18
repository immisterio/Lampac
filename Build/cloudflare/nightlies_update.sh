#!/usr/bin/env bash

VERSION=$1
if [ -n "$VERSION" ]; then
    UPDATEURI="https://${VERSION}.bwa.pages.dev/lpc/update.zip"
else
    UPDATEURI="https://bwa.pages.dev/lpc/update.zip"
fi

cd /home/lampac || { echo "Failed to change directory to /home/lampac. Exiting."; exit 1; }

rm -f update.zip

echo "Download $UPDATEURI"

if ! curl -L -k -o update.zip "$UPDATEURI"; then
	echo "\n\nFailed to download update.zip. Exiting."
	exit 1
fi
if ! unzip -t update.zip; then
	echo "\n\nFailed to test update.zip. Exiting."
	exit 1
fi

systemctl stop lampac || { echo "\n\nFailed to stop lampac service. Exiting."; exit 1; }
unzip -o update.zip
rm -f update.zip
systemctl start lampac || { echo "\n\nFailed to start lampac service. Exiting."; exit 1; }

echo "\n\nUpdate completed successfully."
