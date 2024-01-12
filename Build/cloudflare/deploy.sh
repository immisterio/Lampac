curl -sSL https://dot.net/v1/dotnet-install.sh > dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 7.0.1xx -InstallDir ./dotnet7
./dotnet-install.sh --channel 6.0.1xx -InstallDir ./dotnet6

chmod +x Build/cloudflare/nightlies.sh
./Build/cloudflare/nightlies.sh


####### BwaJS ####### 

cat Build/cloudflare/Shared.Engine.csproj > Shared.Engine/Shared.Engine.csproj
cat Build/cloudflare/Shared.Model.csproj > Shared.Model/Shared.Model.csproj

./dotnet7/dotnet workload install wasm-tools
./dotnet7/dotnet publish JinEnergy -c Release

mkdir -p out/

cp -R Build/cloudflare/functions .
cp -R JinEnergy/bin/Release/net7.0/publish/wwwroot/_framework/* out/

if test -f "out/JinEnergy.dll"; then
	cat Build/cloudflare/_headers > out/_headers

	cd out/
	rm -f *.gz *.br
	python -m zipfile -c latest.zip *
	
	mkdir -p lpc
	cp ../lpc/update.zip lpc/
fi
