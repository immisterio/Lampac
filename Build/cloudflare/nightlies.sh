curl -sSL https://dot.net/v1/dotnet-install.sh > dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 6.0.1xx -InstallDir ./dotnet

./dotnet/dotnet publish Lampac -c Release
cp -R Lampac/bin/Release/net6.0/publish/* out/

mkdir -p out/module

./dotnet/dotnet publish DLNA -c Release
cp Lampac/DLNA/bin/Release/net6.0/publish/DLNA.dll out/module/

./dotnet/dotnet publish JacRed -c Release
cp Lampac/JacRed/bin/Release/net6.0/publish/JacRed.dll out/module/

./dotnet/dotnet publish Merchant -c Release
cp Lampac/Merchant/bin/Release/net6.0/publish/Merchant.dll out/module/

./dotnet/dotnet publish Online -c Release
cp Lampac/Online/bin/Release/net6.0/publish/Online.dll out/module/

./dotnet/dotnet publish SISI -c Release
cp Lampac/SISI/bin/Release/net6.0/publish/SISI.dll out/module/

./dotnet/dotnet publish TorrServer -c Release
cp Lampac/TorrServer/bin/Release/net6.0/publish/TorrServer.dll out/module/

./dotnet/dotnet publish Tracks -c Release
cp Lampac/Tracks/bin/Release/net6.0/publish/Tracks.dll out/module/

cd out/
python -m zipfile -c update.zip *
