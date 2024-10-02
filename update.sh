#!/usr/bin/env bash
DEST="/home/lampac"
cd $DEST

ver=$(cat vers.txt)
gitver=$(curl -k -s https://api.github.com/repos/immisterio/Lampac/releases/latest | grep tag_name | sed s/[^0-9]//g)
if [ $gitver -gt $ver ]; then
    systemctl stop lampac
    echo "update lampac to version $gitver ..."
    rm -f update.zip
    curl -L -k -o update.zip https://github.com/immisterio/Lampac/releases/latest/download/update.zip
    unzip -o update.zip
    rm -f update.zip
    echo -n $gitver > vers.txt
    systemctl start lampac
else
    check_ping() {
        response=$(curl -k -s "$1/ping")
        if [[ "$response" == *"pong"* ]]; then
            return 0
        else
            return 1
        fi
    }

    if check_ping "http://noah.lampac.sh"; then
        BASE_URL="http://noah.lampac.sh"
    elif check_ping "https://lampac.sh"; then
        BASE_URL="https://lampac.sh"
    else
        echo "minor updates are not available"
        exit 1
    fi

    mver=$(cat vers-minor.txt)
    dver=$(curl -k -s $BASE_URL/update/$ver.txt)
	
    if [[ ${#dver} -eq 8 && $dver != $mver ]]; then
        systemctl stop lampac
        echo "update lampac to version $gitver ..."
        rm -f update.zip
        curl -L -k -o update.zip $BASE_URL/update/$dver.zip
        unzip -o update.zip
        rm -f update.zip
        echo -n $dver > vers-minor.txt
        systemctl start lampac
    else
        echo "lampac already current version $ver"
    fi
fi
