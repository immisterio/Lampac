curl -sSL https://dot.net/v1/dotnet-install.sh > dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --version 9.0.304 -InstallDir ./dotnet

chmod +x Build/cloudflare/nightlies.sh
./Build/cloudflare/nightlies.sh

cd out/

mkdir -p lpc
cp -R ../lpc/* lpc/
cp lpc/update.sh ver.sh

#mkdir -p debug
#mv ../debug/debug.zip .
#mv ../debug/debug.sh .
