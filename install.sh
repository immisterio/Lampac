#!/usr/bin/bash
DEST="/home"

# Become root
# sudo su -
apt-get update && apt-get install -y wget unzip

# Install .NET
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && chmod 755 dotnet-install.sh && ./dotnet-install.sh
echo "export DOTNET_ROOT=\$HOME/.dotnet" >> ~/.bashrc
echo "export PATH=\$PATH:\$HOME/.dotnet:\$HOME/.dotnet/tools" >> ~/.bashrc
source ~/.bashrc

# Download zip
cd $DEST && rm -rf lampac && mkdir lampac && cd lampac
wget https://github.com/immisterio/Lampac/releases/download/lam5/publish.zip
unzip publish.zip
rm -f publish.zip

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
ExecStart=$HOME/.dotnet/dotnet Lampac.dll
#ExecReload=/bin/kill -s HUP $MAINPID
#ExecStop=/bin/kill -s QUIT $MAINPID
Restart=always
[Install]
WantedBy=multi-user.target
EOF

# Enable service
systemctl daemon-reload
systemctl enable lampac
systemctl start lampac

# Note
echo ""
echo "Please check / edit $DEST/lampac/init.conf params and configure it"
echo ""
echo "Then [re]start it as systemctl [re]start lampac"
echo ""
echo "Have fun!"
