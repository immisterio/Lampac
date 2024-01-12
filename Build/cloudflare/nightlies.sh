mkdir -p lpc/

./dotnet-install.sh --channel 6.0.1xx -InstallDir ./dotnet6

./dotnet6/dotnet publish Lampac -c Release
cp -R Lampac/bin/Release/net6.0/publish/* lpc/

mkdir -p lpc/module

./dotnet6/dotnet publish DLNA -c Release
cp Lampac/DLNA/bin/Release/net6.0/publish/DLNA.dll lpc/module/

./dotnet6/dotnet publish JacRed -c Release
cp Lampac/JacRed/bin/Release/net6.0/publish/JacRed.dll lpc/module/

./dotnet6/dotnet publish Merchant -c Release
cp Lampac/Merchant/bin/Release/net6.0/publish/Merchant.dll lpc/module/

./dotnet6/dotnet publish Online -c Release
cp Lampac/Online/bin/Release/net6.0/publish/Online.dll lpc/module/

./dotnet6/dotnet publish SISI -c Release
cp Lampac/SISI/bin/Release/net6.0/publish/SISI.dll lpc/module/

./dotnet6/dotnet publish TorrServer -c Release
cp Lampac/TorrServer/bin/Release/net6.0/publish/TorrServer.dll lpc/module/

./dotnet6/dotnet publish Tracks -c Release
cp Lampac/Tracks/bin/Release/net6.0/publish/Tracks.dll lpc/module/

cd lpc/
python -m zipfile -c update.zip *

cd ../
mkdir -p out/lpc
cp lpc/update.zip out/lpc/
