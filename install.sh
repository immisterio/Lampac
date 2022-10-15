#!/usr/bin/bash
DEST="/home"
REPO="https://github.com/immisterio/lampac.git"
# Become root
# sudo su -
apt-get update && apt-get install -y wget git
# Install .NET
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && chmod 755 dotnet-install.sh && ./dotnet-install.sh
echo "export DOTNET_ROOT=\$HOME/.dotnet" >> ~/.bashrc
echo "export PATH=\$PATH:\$HOME/.dotnet:\$HOME/.dotnet/tools" >> ~/.bashrc
source ~/.bashrc
# Clone repo and build
cd $DEST && rm -rf lampac && git clone $REPO && cd lampac
dotnet build -c Release -o bin
dotnet publish -c Release --self-contained --runtime linux-x64 -o bin
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
WorkingDirectory=$DEST/lampac
ExecStart=$DEST/lampac/bin/Lampac
#ExecReload=/bin/kill -s HUP $MAINPID
#ExecStop=/bin/kill -s QUIT $MAINPID
Restart=always

[Install]
WantedBy=multi-user.target
EOF
# Enable service
systemctl daemon-reload
systemctl enable lampac
# Configure JacRed
mkdir -p $DEST/lampac/cache/html
cat <<EOF > $DEST/lampac/init.conf
{
  "timeoutSeconds": 5,
  "htmlCacheToMinutes": 1,
  "magnetCacheToMinutes": 2,
  "apikey": "",
  "Rutor": {
    "host": "http://rutor.info",
    "enable": true,
    "useproxy": false
  },
  "TorrentBy": {
    "host": "http://torrent.by",
    "enable": true,
    "useproxy": false
  },
  "Kinozal": {
    "host": "http://kinozal.tv",
    "enable": true,
    "useproxy": false
  },
  "NNMClub": {
    "host": "https://nnmclub.to",
    "enable": true,
    "useproxy": false
  },
  "Bitru": {
    "host": "https://bitru.org",
    "enable": true,
    "useproxy": false
  },
  "Toloka": {
    "host": "https://toloka.to",
    "enable": false,
    "login": {
      "u": "user",
      "p": "passwd"
    }
  },
  "Rutracker": {
    "host": "https://rutracker.net",
    "enable": false,
    "login": {
      "u": "user",
      "p": "passwd"
    }
  },
  "Underverse": {
    "host": "https://underver.se",
    "enable": false,
    "login": {
      "u": "user",
      "p": "passwd"
    }
  },
  "BongaCams": {
    "host": "https://rt.bongacams.com",
    "enable": true,
    "useproxy": false
  },
  "Chaturbate": {
    "host": "https://chaturbate.com",
    "enable": true,
    "useproxy": false
  },
  "Ebalovo": {
    "host": "https://www.ebalovo.pro",
    "enable": true,
    "useproxy": false
  },
  "Eporner": {
    "host": "https://www.eporner.com",
    "enable": true,
    "useproxy": false
  },
  "HQporner": {
    "host": "https://hqporner.com",
    "enable": true,
    "useproxy": false
  },
  "Porntrex": {
    "host": "https://www.porntrex.com",
    "enable": true,
    "useproxy": false
  },
  "Spankbang": {
    "host": "https://ru.spankbang.com",
    "enable": true,
    "useproxy": false
  },
  "Xhamster": {
    "host": "https://ru.xhamster.com",
    "enable": true,
    "useproxy": false
  },
  "Xnxx": {
    "host": "https://www.xnxx.com",
    "enable": true,
    "useproxy": false
  },
  "Xvideos": {
    "host": "https://www.xvideos.com",
    "enable": true,
    "useproxy": false
  },
  "proxy": {
    "useAuth": false,
    "BypassOnLocal": false,
    "username": "",
    "password": "",
    "list": [
      "ip:port",
      "socks5://ip:port"
 ]
  }
}
EOF
systemctl start lampac
# Note
echo ""
echo "Please check / edit $DEST/lampac/init.conf params and configure it"
echo ""
echo "Then [re]start it as systemctl [re]start lampac"
echo ""
echo "Have fun!"