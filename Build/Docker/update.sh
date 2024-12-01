#!/usr/bin/env bash

ver=$(curl -k -s https://api.github.com/repos/immisterio/Lampac/releases/latest | grep tag_name | sed s/[^0-9]//g)
upver=$(curl -k -s http://noah.lampac.sh/update/$ver.txt)
	
if [[ ${#upver} -eq 8 ]]; then
	curl -L -k -o update.zip http://noah.lampac.sh/update/$upver.zip
	unzip -o update.zip
	rm -f update.zip
fi
