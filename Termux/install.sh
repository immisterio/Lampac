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

# Clean packages cache
apt-get clean && rm -rf /var/lib/apt/lists/*

# Download zip
curl -L -k -o publish.zip https://github.com/immisterio/Lampac/releases/latest/download/publish.zip
unzip -o publish.zip
rm -f publish.zip

echo -n "admin" > passwd

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
rm -rf merchant torrserver wwwroot/bwa
rm -rf runtimes/wi*
rm -rf runtimes/os*
rm -rf runtimes/linux-m*
rm -rf runtimes/linux-arm
rm -rf runtimes/linux-x64

#exit from Debian
exit

ln -s /data/data/com.termux/files/usr/var/lib/proot-distro/installed-rootfs/debian/ debian
tmux new-session -d -s Lampac "proot-distro login debian -- dotnet Lampac.dll"'

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
