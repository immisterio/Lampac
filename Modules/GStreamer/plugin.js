(function () {
    function account(url) {
        url = url + '';
        if (url.indexOf('account_email=') == -1) {
            var email = Lampa.Storage.get('account_email');
            if (email) url = Lampa.Utils.addUrlComponent(url, 'account_email=' + encodeURIComponent(email));
        }
        if (url.indexOf('uid=') == -1) {
            var uid = Lampa.Storage.get('lampac_unic_id', '');
            if (uid) url = Lampa.Utils.addUrlComponent(url, 'uid=' + encodeURIComponent(uid));
        }
        if (url.indexOf('token=') == -1) {
            var token = '{token}';
            if (token != '') url = Lampa.Utils.addUrlComponent(url, 'token={token}');
        }
        return url;
    }

    function resolveMediaUrl(data) {
        if (data && data.url) return data.url;
        return '';
    }

    function isMkvSource(data) {
        var url = resolveMediaUrl(data);
        if (!url) return false;

        url = url.split('#')[0].split('?')[0];

        return /\.mkv$/i.test(url) || /\/lite\/pidtor\//i.test(url);
    }

    function handlePlayerStart(e) {
        if (isMkvSource(e.data)) {
            if (e.data.url.indexOf('/gst/') != -1 || e.data.url.indexOf('.m3u8') != -1)
                return;

            e.abort()

            setTimeout(() => {
                Lampa.Player.close()

                var src = e.data.url.replace(/&(preload|stat|m3u)/g, '&play');

                delete e.data.torrent_hash;
                e.data.hls_type = 'hlsjs';
                e.data.hls_manifest_timeout = 30000;
                e.data.url = account('{localhost}/gst/start.m3u8?link=' + encodeURIComponent(src));

                var playback = e.data;
                Lampa.Player.play(playback);
            }, 10);
        }
    }

    if (!window.lampac_transcoding_plugin) {
        window.lampac_transcoding_plugin = true;
        Lampa.Player.listener.follow('create', handlePlayerStart);
    }
})();