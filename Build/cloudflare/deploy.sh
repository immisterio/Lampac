#cat Build/cloudflare/JinEnergy.csproj > JinEnergy/JinEnergy.csproj
cat Build/cloudflare/Shared.Engine.csproj > Shared.Engine/Shared.Engine.csproj
cat Build/cloudflare/Shared.Model.csproj > Shared.Model/Shared.Model.csproj

curl -sSL https://dot.net/v1/dotnet-install.sh > dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 7.0.1xx -InstallDir ./dotnet
./dotnet/dotnet workload install wasm-tools
./dotnet/dotnet publish JinEnergy -c Release

mkdir -p out/aot/

cp -R Build/cloudflare/functions .
cat Build/cloudflare/_headers > out/_headers
cp -R JinEnergy/bin/Release/net7.0/publish/wwwroot/_framework/* out/

apt-get install -y zip
cd out/
zip latest.zip *
