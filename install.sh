#!/usr/bin/env bash
DEST="/home/lampac"

# Become root
# sudo su -
apt-get update
apt-get install -y wget unzip ffmpeg
apt-get install -y libnss3-dev libgdk-pixbuf2.0-dev libgtk-3-dev libxss-dev

# Install .NET
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh 
chmod 755 dotnet-install.sh
./dotnet-install.sh --channel 6.0 --runtime aspnetcore
#echo "export DOTNET_ROOT=\$HOME/.dotnet" >> ~/.bashrc
#echo "export PATH=\$PATH:\$HOME/.dotnet:\$HOME/.dotnet/tools" >> ~/.bashrc
#source ~/.bashrc

# Download zip
mkdir $DEST -p 
cd $DEST
wget https://github.com/immisterio/Lampac/releases/latest/download/publish.zip
unzip -o publish.zip
rm -f publish.zip

# automatic updates
curl -s https://api.github.com/repos/immisterio/Lampac/releases/latest | grep tag_name | sed s/[^0-9]//g > $DEST/vers.txt
curl -s https://raw.githubusercontent.com/immisterio/lampac/main/update.sh > $DEST/update.sh
chmod 755 $DEST/update.sh
crontab -l | { cat; echo "10 */4 * * * /bin/bash $DEST/update.sh"; } | crontab -

# update minor
/bin/bash $DEST/update.sh
cd $DEST

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

# Note
echo ""
echo "################################################################"
echo ""
echo "Have fun!"
echo ""
echo "Please check/edit $DEST/init.conf params and configure it"
echo ""
echo "Then [re]start it as systemctl [re]start lampac"
echo ""
echo "Clear iptables if port 9118 is not available"
echo "bash $DEST/iptables-drop.sh"
echo ""
