mkdir -p debug/

./dotnet6/dotnet publish Lampac -c Debug
rm -rf Lampac/bin/Debug/net6.0/publish/runtimes
cp -R Lampac/bin/Debug/net6.0/publish/* debug/

mkdir -p debug/module

./dotnet6/dotnet publish DLNA -c Debug
cp DLNA/bin/Debug/net6.0/publish/DLNA.dll debug/module/

./dotnet6/dotnet publish JacRed -c Debug
cp JacRed/bin/Debug/net6.0/publish/JacRed.dll debug/module/

./dotnet6/dotnet publish Merchant -c Debug
cp Merchant/bin/Debug/net6.0/publish/Merchant.dll debug/module/

./dotnet6/dotnet publish Online -c Debug
cp Online/bin/Debug/net6.0/publish/Online.dll debug/module/

./dotnet6/dotnet publish SISI -c Debug
cp SISI/bin/Debug/net6.0/publish/SISI.dll debug/module/

./dotnet6/dotnet publish TorrServer -c Debug
cp TorrServer/bin/Debug/net6.0/publish/TorrServer.dll debug/module/

./dotnet6/dotnet publish Tracks -c Debug
cp Tracks/bin/Debug/net6.0/publish/Tracks.dll debug/module/

cd debug/
rm -f Lampac.runtimeconfig.json
python -m zipfile -c debug.zip *

cd ../
cat Build/cloudflare/nightlies_update_debug.sh > debug/debug.sh