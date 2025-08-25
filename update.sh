#!/usr/bin/env bash
DEST="/home/lampac"
cd $DEST

VERSION=$1
if [ -n "$VERSION" ]; then
    echo "update lampac to version $VERSION"
    rm -f update.zip
    if ! curl -L -k -o update.zip "http://noah.lampac.sh/update/$VERSION.zip"; then
        echo "Failed to download update.zip. Exiting."
        exit 1
    fi
    if ! unzip -t update.zip; then
        echo "Failed to test update.zip. Exiting."
        exit 1
    fi
    systemctl stop lampac
    unzip -o update.zip
    rm -f update.zip
    echo -n $VERSION > data/vers-minor.txt
    systemctl start lampac
    exit
fi

ver=$(cat data/vers.txt)
gitver=$(curl --connect-timeout 10 -m 20 -k -s https://api.github.com/repos/immisterio/Lampac/releases/latest | grep tag_name | sed s/[^0-9]//g)
if [ $gitver -gt $ver ]; then
    echo "update lampac to version $gitver"
    rm -f update.zip
    if ! curl -L -k -o update.zip https://github.com/immisterio/Lampac/releases/latest/download/update.zip; then
        echo "Failed to download update.zip. Exiting."
        exit 1
    fi
    if ! unzip -t update.zip; then
        echo "Failed to test update.zip. Exiting."
        exit 1
    fi
    systemctl stop lampac
    unzip -o update.zip
    rm -f update.zip
    echo -n $gitver > data/vers.txt
    systemctl start lampac
else
    check_ping() {
        response=$(curl --connect-timeout 5 -m 10 -k -s "$1/ping")
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

    mver=$(cat data/vers-minor.txt)
    dver=$(curl -k -s $BASE_URL/update/$ver.txt)
	
    if [[ ${#dver} -eq 8 && $dver != $mver ]]; then
        echo "update lampac to version $gitver.$mver"
        rm -f update.zip
        if ! curl -L -k -o update.zip "$BASE_URL/update/$dver.zip"; then
            echo "Failed to download update.zip. Exiting."
            exit 1
        fi
        if ! unzip -t update.zip; then
            echo "Failed to test update.zip. Exiting."
            exit 1
        fi
        systemctl stop lampac
        unzip -o update.zip
        rm -f update.zip
        echo -n $dver > data/vers-minor.txt
        systemctl start lampac
    else
        echo "lampac already current version $ver"
    fi
fi


# clear
rm -rf runtimes/wi*
rm -rf runtimes/os*
