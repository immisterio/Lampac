cd /opt/buildhome/repo

cat Build/cloudflare/JinEnergy.csproj > JinEnergy/JinEnergy.csproj
cat Build/cloudflare/Shared.Engine.csproj > Shared.Engine/Shared.Engine.csproj
cat Build/cloudflare/Shared.Model.csproj > Shared.Model/Shared.Model.csproj

curl -sSL https://dot.net/v1/dotnet-install.sh > dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh -InstallDir /opt/buildhome/repo/dotnet
/opt/buildhome/repo/dotnet/dotnet workload install wasm-tools

cd /opt/buildhome/repo/JinEnergy
/opt/buildhome/repo/dotnet/dotnet publish -c Release
cd /opt/buildhome/repo

mkdir -p out/aot/
cp -R Build/cloudflare/functions .
cp -R JinEnergy/bin/Release/net8.0/wwwroot/_framework/* out/aot/