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

# Delete previous version
systemctl stop lampac
systemctl disable lampac
systemctl daemon-reload
rm -f /etc/systemd/system/lampac.service
mv $DEST/lampac/init.conf ~/init.conf

# Download zip
cd $DEST && rm -rf lampac && mkdir lampac && cd lampac
wget https://github.com/immisterio/Lampac/releases/latest/download/publish.zip
unzip publish.zip
rm -f publish.zip
mv ~/init.conf init.conf.back

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
echo "Please check/edit $DEST/lampac/init.conf params and configure it"
echo ""
echo "Then [re]start it as systemctl [re]start lampac"
echo ""
echo "Clear iptables if port 9118 is not available"
echo "bash $DEST/lampac/iptables-drop.sh"
echo ""
