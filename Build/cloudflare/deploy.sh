#cat /etc/issue

#cat Build/cloudflare/JinEnergy.csproj > JinEnergy/JinEnergy.csproj
cat Build/cloudflare/Shared.Engine.csproj > Shared.Engine/Shared.Engine.csproj
cat Build/cloudflare/Shared.Model.csproj > Shared.Model/Shared.Model.csproj

curl -sSL https://dot.net/v1/dotnet-install.sh > dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 7.0.1xx -InstallDir ./dotnet
./dotnet/dotnet workload install wasm-tools
./dotnet/dotnet publish JinEnergy -c Release

mkdir -p out/

cp -R Build/cloudflare/functions .
cat Build/cloudflare/_headers > out/_headers
cp -R JinEnergy/bin/Release/net7.0/publish/wwwroot/_framework/* out/

if test -f "out/JinEnergy.dll"; then
	cd out/
	rm -f *.gz *.br
	python -m zipfile -c latest.zip *
	
	cd ../
	chmod +x Build/cloudflare/nightlies.sh
	./Build/cloudflare/nightlies.sh
fi
