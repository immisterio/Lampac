#!/bin/bash

pkg install tmux proot-distro -y
proot-distro install debian

# Start Debian
proot-distro login debian

# Install packages
apt-get update
apt-get install -y curl unzip
apt-get install -y libicu-dev
apt-get install -y libicu72
apt-get install -y libicu76

# Install .NET 9
curl -L -k -o dotnet-install.sh https://dot.net/v1/dotnet-install.sh
chmod 755 dotnet-install.sh
./dotnet-install.sh --channel 9.0 --runtime aspnetcore --install-dir /usr/share/dotnet
ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
rm dotnet-install.sh

# clear .NET 6
rm -f *.dll
rm -f *.pdb
rm -f GeoLite2-Country.mmdb vers-minor.txt
rm -rf runtimes nginx
rm -rf .playwright

for lang in cs de es fr it ja ko pl pt-BR ru tr zh-Hans zh-Hant; do
    rm -rf $lang
done

# Download zip
curl -L -k -o publish.zip https://github.com/immisterio/Lampac/releases/latest/download/publish.zip
unzip -o publish.zip
rm -f publish.zip

echo -n "termux" > passwd

# init.conf
cat <<EOF > init.conf
{
  "typecache": "mem",
  "mikrotik": true,
  "pirate_store": false,
  "listen": {
    "compression": false
  },
  "chromium": {
    "enable": false
  },
  "firefox": {
    "enable": false
  },
  "dlna": {
    "enable": false,
    "autoupdatetrackers": false
  },
  "cub": {
    "enable": false
  },
  "tmdb": {
    "enable": false
  },
  "weblog": {
    "enable": true
  },
  "LampaWeb": {
    "initPlugins": {
      "dlna": false,
      "tracks": false,
      "tmdbProxy": false,
      "online": true,
      "sisi": true,
      "timecode": true,
      "torrserver": false,
      "backup": true,
      "sync": false
    }
  },
  "serverproxy": {
    "enable": true,
    "verifyip": false,
    "encrypt_aes": true,
    "image": {
      "cache": false,
      "cache_rsize": false
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
  },
  "Spankbang": {
    "rhub": true
  },
  "BongaCams": {
    "rhub": true
  },
  "Runetki": {
    "rhub": true
  },
  "VDBmovies": {
    "rhub": true,
    "spider": false
  },
  "VideoDB": {
    "rhub": true
  },
  "FanCDN": {
    "rhub": true
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

# update info
curl -k -s https://api.github.com/repos/immisterio/Lampac/releases/latest | grep tag_name | sed s/[^0-9]//g > data/vers.txt
echo -n "1" > data/vers-minor.txt

# update.sh
cat <<EOF > update.sh
#!/usr/bin/env bash

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
    unzip -o update.zip
    rm -f update.zip
    echo -n $gitver > data/vers.txt
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
        unzip -o update.zip
        rm -f update.zip
        echo -n $dver > data/vers-minor.txt
    else
        echo "lampac already current version $ver"
    fi
fi

rm -f data/GeoLite2-Country.mmdb
rm -rf .playwright merchant torrserver wwwroot/bwa
rm -rf data/widgets
rm -rf runtimes/wi*
rm -rf runtimes/os*
rm -rf runtimes/linux-m*
rm -rf runtimes/linux-arm
rm -rf runtimes/linux-x64
EOF

# update minor
/bin/bash update.sh

# Lampac.runtimeconfig.json
cat <<EOF > Lampac.runtimeconfig.json
{
  "runtimeOptions": {
    "tfm": "net9.0",
    "frameworks": [
      {
        "name": "Microsoft.NETCore.App",
        "version": "9.0.0"
      },
      {
        "name": "Microsoft.AspNetCore.App",
        "version": "9.0.0"
      }
    ],
    "configProperties": {
      "System.GC.Server": false,
      "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
      "System.Reflection.NullabilityInfoContext.IsSupported": true,
      "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false
    }
  }
}
EOF

# clear
rm -f data/GeoLite2-Country.mmdb
rm -rf .playwright merchant torrserver wwwroot/bwa
rm -rf data/widgets
rm -rf runtimes/wi*
rm -rf runtimes/os*
rm -rf runtimes/linux-m*
rm -rf runtimes/linux-arm
rm -rf runtimes/linux-x64

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
