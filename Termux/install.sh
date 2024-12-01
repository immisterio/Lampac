#!/bin/bash

pkg install tmux proot-distro -y
proot-distro install debian

# Start Debian
proot-distro login debian

# Install packages
apt-get update
apt-get install -y libicu72 curl unzip

# Install .NET
curl -L -k -o dotnet-install.sh https://dot.net/v1/dotnet-install.sh
chmod 755 dotnet-install.sh
./dotnet-install.sh --channel 6.0 --runtime aspnetcore --install-dir /usr/share/dotnet
ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
rm dotnet-install.sh

# Download zip
curl -L -k -o publish.zip https://github.com/immisterio/Lampac/releases/latest/download/publish.zip
unzip -o publish.zip
rm -f publish.zip

echo -n "termux" > passwd

# init.conf
cat <<EOF > init.conf
{
  "listenport": 9118,
  "typecache": "mem",
  "mikrotik": true,
  "puppeteer": {
    "enable": false
  },
  "dlna": {
    "enable": false,
    "autoupdatetrackers": false
  },
  "weblog": {
    "enable": true
  },
  "serverproxy": {
    "enable": false,
    "verifyip": false,
    "cache": {
      "img": false,
      "img_rsize": false
    },
    "buffering": {
      "enable": false
    }
  },
  "online": {
    "checkOnlineSearch": false
  },
  "sisi": {
    "rsize": false
  }
}
EOF

# manifest.json
cat <<EOF > module/manifest.json
[
  {
    "enable": true,
    "dll": "SISI.dll"
  },
  {
    "enable": true,
    "dll": "Online.dll"
  }
]
EOF

# Lampac.runtimeconfig.json
cat <<EOF > Lampac.runtimeconfig.json
{
  "runtimeOptions": {
    "tfm": "net6.0",
    "frameworks": [
      {
        "name": "Microsoft.NETCore.App",
        "version": "6.0.0"
      },
      {
        "name": "Microsoft.AspNetCore.App",
        "version": "6.0.0"
      }
    ],
    "configProperties": {
      "System.GC.Server": false,
      "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
      "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false,
      "System.GC.HeapHardLimit": 50000000
    }
  }
}
EOF

# clear
rm -f GeoLite2-Country.mmdb
rm -rf merchant torrserver wwwroot/bwa
rm -rf runtimes/wi*
rm -rf runtimes/os*
rm -rf runtimes/linux-m*
rm -rf runtimes/linux-arm
rm -rf runtimes/linux-x64

# update info
curl -k -s https://api.github.com/repos/immisterio/Lampac/releases/latest | grep tag_name | sed s/[^0-9]//g > vers.txt
echo -n "1" > vers-minor.txt

# update.sh
cat <<EOF > update.sh
#!/usr/bin/env bash

ver=$(cat vers.txt)
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
    unzip -o update.zip
    rm -f update.zip
    echo -n $gitver > vers.txt
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

    mver=$(cat vers-minor.txt)
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
        unzip -o update.zip
        rm -f update.zip
        echo -n $dver > vers-minor.txt
    else
        echo "lampac already current version $ver"
    fi
fi
EOF

# update minor
/bin/bash update.sh

# Clean packages cache
apt-get clean && rm -rf /var/lib/apt/lists/*

#exit from Debian
exit

cat <<EOF > start.sh
#!/bin/bash

tmux new-session -d -s Lampac "proot-distro login debian -- dotnet Lampac.dll"
EOF

cat <<EOF > stop.sh
#!/bin/bash

tmux kill-session -a -t Lampac
EOF

cat <<EOF > restart.sh
#!/bin/bash

bash stop.sh
bash start.sh
EOF

cat <<EOF > update.sh
#!/bin/bash

proot-distro login debian
bash update.sh
exit
EOF

# Run Motherfucker Run 
ln -s /data/data/com.termux/files/usr/var/lib/proot-distro/installed-rootfs/debian/ debian
tmux new-session -d -s Lampac "proot-distro login debian -- dotnet Lampac.dll"

# Note
echo ""
echo "################################################################"
echo ""
echo "Have fun!"
echo ""
echo "http://127.0.0.1:9118"
echo ""
echo "Please check/edit http://127.0.0.1:9118/admin/init params and configure it"
echo ""
