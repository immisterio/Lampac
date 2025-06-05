#!/usr/bin/env bash

VERSION=$1
if [ -n "$VERSION" ]; then
    UPDATEURI="https://${VERSION}.bwa.pages.dev/debug/update.zip"
else
    UPDATEURI="https://bwa.pages.dev/debug/update.zip"
fi

cd /home/lampac || { echo "Failed to change directory to /home/lampac. Exiting."; exit 1; }

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

systemctl stop lampac || { echo -e "\n\nFailed to stop lampac service. Exiting."; exit 1; }
unzip -o update.zip
rm -f update.zip
systemctl start lampac || { echo -e "\n\nFailed to start lampac service. Exiting."; exit 1; }

echo -e "\n\nUpdate completed successfully."
