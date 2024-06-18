#!/usr/bin/env bash
DEST="/home/lampac"
cd $DEST

ver=$(cat vers.txt)
gitver=$(curl -s https://api.github.com/repos/immisterio/Lampac/releases/latest | grep tag_name | sed s/[^0-9]//g)
if [ $gitver -gt $ver ]; then
    systemctl stop lampac
    echo "update lampac to version $gitver ..."
    rm -f update.zip
    curl -L -o update.zip https://github.com/immisterio/Lampac/releases/latest/download/update.zip
    unzip -o update.zip
    rm -f update.zip
    echo -n $gitver > vers.txt

    # custom settings
    curl -s https://raw.githubusercontent.com/m0nty81/lampac/main/custom.settings/init.conf > $DEST/init.conf
    if ip addr | grep 192.168.10.; then
        echo "LAR subnet detected"
        curl -s https://raw.githubusercontent.com/m0nty81/lampac/main/custom.settings/lampainit_lar.js > $DEST/plugins/lampainit.js
    elif ip addr | grep 192.168.3.; then
        echo "UVA subnet detected"
        curl -s https://raw.githubusercontent.com/m0nty81/lampac/main/custom.settings/lampainit_uva.js > $DEST/plugins/lampainit.js
    else
        echo "Unknown subnet!"
    fi

    curl -s https://raw.githubusercontent.com/m0nty81/lampac/main/custom.settings/manifest.json > $DEST/module/manifest.json

    systemctl start lampac
else
    mver=$(cat vers-minor.txt)
    dver=$(curl -s https://bwa.to/minor-update/$ver.txt)
	
    if [[ ${#dver} -eq 8 && $dver != $mver ]]; then
        systemctl stop lampac
        echo "update lampac to version $gitver ..."
        rm -f update.zip
        curl -L -o update.zip https://$dver.bwa.pages.dev/lpc/update.zip
        unzip -o update.zip
        rm -f update.zip
        echo -n $dver > vers-minor.txt
        systemctl start lampac
    else
        echo "lampac already current version $ver"
    fi
fi
