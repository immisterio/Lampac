#!/usr/bin/env bash
DEST="/home/lampac"

# Become root
# sudo su -
apt-get update
apt-get install -y unzip curl coreutils libicu-dev

# Install .NET
if ! curl -L -k -o dotnet-install.sh https://dot.net/v1/dotnet-install.sh; then
   echo "Failed to download dotnet-install.sh. Exiting."
   exit 1
fi

chmod 755 dotnet-install.sh
./dotnet-install.sh --channel 9.0 --runtime aspnetcore --install-dir /usr/share/dotnet
ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

# Download zip
mkdir $DEST -p 
cd $DEST
if ! curl -L -k -o publish.zip https://github.com/immisterio/Lampac/releases/latest/download/publish.zip; then
   echo "Failed to download publish.zip. Exiting."
   exit 1
fi

unzip -o publish.zip
rm -f publish.zip

# automatic updates
curl -k -s https://api.github.com/repos/immisterio/Lampac/releases/latest | grep tag_name | sed s/[^0-9]//g > $DEST/data/vers.txt
curl -k -s https://raw.githubusercontent.com/immisterio/lampac/main/update.sh > $DEST/update.sh
chmod 755 $DEST/update.sh
#crontab -l | { cat; echo "$(shuf -i 10-55 -n 1) * * * * /bin/bash $DEST/update.sh"; } | crontab -

CRON_JOB="$(shuf -i 10-55 -n 1) * * * * /bin/bash $DEST/update.sh"
(crontab -l | grep -vF "/bin/bash $DEST/update.sh"; echo "$CRON_JOB") | crontab -

# init.conf
random_port=$(shuf -i 9000-12999 -n 1)
cat <<EOF > $DEST/init.conf
"listen": {
  "port": $random_port
},
"typecache": "mem",
"mikrotik": true,
"chromium": {
  "enable": false
},
"firefox": {
  "enable": false
},
"dlna": {
  "cover": {
    "enable": false
  }
},
"serverproxy": {
  "verifyip": false,
  "image": {
    "cache": false,
    "cache_rsize": false
  },
  "buffering": {
    "enable": false
  }
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
},
"Lumex": {
  "spider": false
}
EOF

# manifest.json
cat <<EOF > $DEST/module/manifest.json
[
  {
    "enable": true,
    "dll": "SISI.dll"
  },
  {
    "enable": true,
    "dll": "Online.dll"
  },
  {
    "enable":true,
    "initspace":"Catalog.ModInit",
    "dll":"Catalog.dll"
  },
  {
    "enable": true,
    "dll": "DLNA.dll"
  },
  {
    "enable": true,
    "initspace": "Jackett.ModInit",
    "dll": "JacRed.dll"
  },
  {
    "enable": false,
    "initspace": "TorrServer.ModInit",
    "dll": "TorrServer.dll"
  }
]
EOF

# Lampac.runtimeconfig.json
cat <<EOF > $DEST/Lampac.runtimeconfig.json
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

# Create service
echo ""
echo "Install service to /etc/systemd/system/lampac.service ..."
touch /etc/systemd/system/lampac.service && chmod 664 /etc/systemd/system/lampac.service
cat <<EOF > /etc/systemd/system/lampac.service
[Unit]
Description=Lampac
Wants=network.target
After=network.target
[Service]
WorkingDirectory=$DEST
ExecStart=/usr/bin/dotnet Lampac.dll
#ExecReload=/bin/kill -s HUP $MAINPID
#ExecStop=/bin/kill -s QUIT $MAINPID
Restart=always
[Install]
WantedBy=multi-user.target
EOF

# Enable service
systemctl daemon-reload
systemctl enable lampac

# update minor
echo -n "1" > $DEST/data/vers-minor.txt
/bin/bash $DEST/update.sh

# clear
cd $DEST
rm -f data/*.json
rm -rf merchant wwwroot/bwa
rm -rf runtimes/wi*
rm -rf runtimes/os*
rm -rf runtimes/linux-m*

# clear runtimes
case $(uname -m) in
    x86_64)
        rm -rf runtimes/linux-a*
        ;;
    armv7l)
        rm -rf runtimes/linux-arm64
		rm -rf runtimes/linux-x64
        ;;
    aarch64)
        rm -rf runtimes/linux-arm
		rm -rf runtimes/linux-x64
        ;;
    *)
        echo ""
        ;;
esac

# done
systemctl start lampac

# iptables drop
cat <<EOF > iptables-drop.sh
#!/bin/sh
echo "Stopping firewall and allowing everyone..."
iptables -F
iptables -X
iptables -t nat -F
iptables -t nat -X
iptables -t mangle -F
iptables -t mangle -X
iptables -P INPUT ACCEPT
iptables -P FORWARD ACCEPT
iptables -P OUTPUT ACCEPT
EOF

get_external_ip() {
   local ip
   ip=$(curl -s --connect-timeout 5 https://api.ipify.org 2>/dev/null)
   if [ -z "$ip" ]; then
      ip=$(curl -s --connect-timeout 5 https://icanhazip.com 2>/dev/null)
   fi
   if [ -z "$ip" ]; then
      ip=$(curl -s --connect-timeout 5 https://ifconfig.me 2>/dev/null)
   fi
   echo "${ip:-IP}"
}

# Note
echo ""
echo "################################################################"
echo ""
echo "Have fun!"
echo ""
echo "http://$(get_external_ip):$random_port"
echo ""
echo "Please check/edit $DEST/init.conf params and configure it"
echo ""
echo "Then [re]start it as systemctl [re]start lampac"
echo ""
echo "Clear iptables if port $random_port is not available"
echo "bash $DEST/iptables-drop.sh"
echo ""
