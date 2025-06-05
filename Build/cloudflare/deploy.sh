curl -sSL https://dot.net/v1/dotnet-install.sh > dotnet-install.sh
chmod +x dotnet-install.sh
#./dotnet-install.sh --version 9.0.200 -InstallDir ./dotnet
#./dotnet-install.sh --version 8.0.110 -InstallDir ./dotnet
./dotnet-install.sh --version 6.0.133 -InstallDir ./dotnet6

chmod +x Build/cloudflare/nightlies.sh
./Build/cloudflare/nightlies.sh

chmod +x Build/cloudflare/nightlies_debug.sh
./Build/cloudflare/nightlies_debug.sh

mkdir -p out/
cp -R Build/cloudflare/functions .
cat Build/cloudflare/_headers > out/_headers

cd out/

mkdir -p lpc
cp -R ../lpc/* lpc/
cp lpc/update.sh ver.sh

mkdir -p debug
cp -R ../debug/* debug/

return;


####### BwaJS ####### 

cat Build/cloudflare/net8/JinEnergy.csproj > JinEnergy/JinEnergy.csproj
cat Build/cloudflare/net8/Shared.Engine.csproj > Shared.Engine/Shared.Engine.csproj
cat Build/cloudflare/net8/Shared.Model.csproj > Shared.Model/Shared.Model.csproj

./dotnet/dotnet workload install wasm-tools
./dotnet/dotnet publish JinEnergy -c Release

mkdir -p out/

cp -R Build/cloudflare/functions .
cp -R JinEnergy/bin/Release/net8.0/publish/wwwroot/_framework/* out/

if test -f "out/blazor.boot.json"; then
	cat Build/cloudflare/_headers > out/_headers

	cd out/
	rm -f *.gz *.br
	python -m zipfile -c latest.zip *
	
	mkdir -p lpc
	cp -R ../lpc/* lpc/
	cp lpc/update.sh ver.sh
fi
