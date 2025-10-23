mkdir -p lpc/

cat Build/cloudflare/Lampac.csproj > Lampac/Lampac.csproj

# Публикация проекта
./dotnet/dotnet publish Lampac -c Release

# Целевая директория
publish_dir="Lampac/bin/Release/net9.0/publish"

# Удаляем все папки в runtimes кроме references
for dir in "$publish_dir/runtimes"/*/; do
    dirname=$(basename "$dir")
    if [ "$dirname" != "references" ]; then
        rm -rf "$dir"
    fi
done

# Перемещаем языковые папки в runtimes/references/
for lang in cs de es fr it ja ko pl pt-BR ru tr zh-Hans zh-Hant; do
    if [ -d "$publish_dir/$lang" ]; then
        mv "$publish_dir/$lang" "$publish_dir/runtimes/references/"
    fi
done

# Копируем всё в lpc/
cp -R "$publish_dir"/* lpc/

# Сборка модулей
mkdir -p lpc/module

./dotnet/dotnet publish DLNA -c Release
cp DLNA/bin/Release/net9.0/publish/DLNA.dll lpc/module/

./dotnet/dotnet publish JacRed -c Release
cp JacRed/bin/Release/net9.0/publish/JacRed.dll lpc/module/

./dotnet/dotnet publish Merchant -c Release
cp Merchant/bin/Release/net9.0/publish/Merchant.dll lpc/module/

./dotnet/dotnet publish Online -c Release
cp Online/bin/Release/net9.0/publish/Online.dll lpc/module/

./dotnet/dotnet publish Catalog -c Release
cp Catalog/bin/Release/net9.0/publish/Catalog.dll lpc/module/

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
mkdir out/
cat Build/cloudflare/nightlies_update.sh > lpc/update.sh
cat lpc/update.sh > out/ver.sh
mv lpc out/