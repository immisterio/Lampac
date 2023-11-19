FROM mcr.microsoft.com/dotnet/sdk:6.0

RUN apt update && apt install -y wget unzip ffmpeg

RUN wget https://github.com/immisterio/Lampac/releases/latest/download/publish.zip && \
	unzip -o publish.zip && \
	rm -f publish.zip

ENTRYPOINT ["dotnet", "Lampac.dll"]