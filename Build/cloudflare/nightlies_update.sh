#!/usr/bin/env bash

VERSION=$1
if [ -n "$VERSION" ]; then
    UPDATEURI="https://${VERSION}.bwa.pages.dev/lpc/update.zip"
else
    UPDATEURI="https://bwa.pages.dev/lpc/update.zip"
fi

cd /home/lampac || { echo "Failed to change directory to /home/lampac. Exiting."; exit 1; }

rm -f update.zip

if ! curl -L -k -o update.zip "$UPDATEURI"; then
	echo "Failed to download update.zip. Exiting."
	exit 1
fi
if ! unzip -t update.zip; then
	echo "Failed to test update.zip. Exiting."
	exit 1
fi

systemctl stop lampac || { echo "Failed to stop lampac service. Exiting."; exit 1; }
unzip -o update.zip
rm -f update.zip
systemctl start lampac || { echo "Failed to start lampac service. Exiting."; exit 1; }

echo "Update completed successfully."
