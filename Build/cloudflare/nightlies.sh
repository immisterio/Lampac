mkdir -p lpc/

./dotnet/dotnet publish Lampac -c Release
rm -rf Lampac/bin/Release/net9.0/publish/runtimes
cp -R Lampac/bin/Release/net9.0/publish/* lpc/

mkdir -p lpc/module

./dotnet/dotnet publish DLNA -c Release
cp DLNA/bin/Release/net9.0/publish/DLNA.dll lpc/module/

./dotnet/dotnet publish JacRed -c Release
cp JacRed/bin/Release/net9.0/publish/JacRed.dll lpc/module/

./dotnet/dotnet publish Merchant -c Release
cp Merchant/bin/Release/net9.0/publish/Merchant.dll lpc/module/

./dotnet/dotnet publish Online -c Release
cp Online/bin/Release/net9.0/publish/Online.dll lpc/module/

./dotnet/dotnet publish SISI -c Release
cp SISI/bin/Release/net9.0/publish/SISI.dll lpc/module/

./dotnet/dotnet publish TorrServer -c Release
cp TorrServer/bin/Release/net9.0/publish/TorrServer.dll lpc/module/

./dotnet/dotnet publish Tracks -c Release
cp Tracks/bin/Release/net9.0/publish/Tracks.dll lpc/module/

cd lpc/
rm -f Lampac.runtimeconfig.json

curl -L -k -o cloudflare.zip "https://lampac.sh/update/cloudflare.zip?v=$(date +%s)"
unzip -o cloudflare.zip
rm -f cloudflare.zip

python -m zipfile -c update.zip *

cd ../
cat Build/cloudflare/nightlies_update.sh > lpc/update.sh
cp lpc/update.sh lpc/ver.sh
mv lpc out/