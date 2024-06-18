(function () {
    'use strict';
	
    var timer = setInterval(function(){
        if(typeof Lampa !== 'undefined'){
            clearInterval(timer);

            if(!Lampa.Storage.get('lampac_initiale','false')) start();
			
            window.lampa_settings.torrents_use = true;
            window.lampa_settings.demo = false;
            window.lampa_settings.read_only = false;
			
            {deny}
			
            {pirate_store}
        }
    },200);
	
	var dcma_timer = setInterval(function(){
	  if(typeof window.lampa_settings != 'undefined' && (window.lampa_settings.fixdcma || window.lampa_settings.dcma)){
		clearInterval(dcma_timer)
		if (window.lampa_settings.dcma)
			window.lampa_settings.dcma = false;
	  }
	},100);

	function start(){
        Lampa.Storage.set('lampac_initiale','true');
        Lampa.Storage.set('source','cub');
        Lampa.Storage.set('proxy_tmdb','true');
        Lampa.Storage.set('poster_size','w500');
        
        Lampa.Storage.set('parser_use','true');
        Lampa.Storage.set('jackett_url','{jachost}');
        Lampa.Storage.set('jackett_key','1');
        Lampa.Storage.set('parser_torrent_type','jackett');
        Lampa.Storage.set('torrserver_use_link','one');
        Lampa.Storage.set('torrserver_url','192.168.10.140:8090'); // LAR IP
        Lampa.Storage.set('torrserver_url_two','192.168.3.240:8090'); // UVA IP
        Lampa.Storage.set('torrserver_auth','false');
        Lampa.Storage.set('internal_torrclient','true');
        Lampa.Storage.set('background_type','complex');
        Lampa.Storage.set('video_quality_default','2160');
        Lampa.Storage.set('Reloadbutton','true');
        Lampa.Storage.set('screensaver','false');
        Lampa.Storage.set('account_use','true');
        Lampa.Storage.set('torrserver_preload','true');

        /*
        Lampa.Storage.set('menu_sort','["Главная","Фильмы","Сериалы","Избранное","История"]');
        Lampa.Storage.set('menu_hide','["Лента","Персона","Аниме","Релизы"]');
        Lampa.Storage.set('torrserver_url_two','192.168.10.140:8090');
        */

        var plugins = Lampa.Plugins.get();

        var plugins_add = [
			{initiale},
            {"url": "{localhost}/plugins/nc.js", "status": 1,"name": "NewCategories", "author": "x"},
            {"url": "{localhost}/plugins/mult.js", "status": 1,"name": "Mult", "author": "x"},
            {"url": "{localhost}/plugins/pubtorr.js", "status": 1,"name": "PublicParsers", "author": "x"},
            {"url": "{localhost}/plugins/ts-preload.js", "status": 1,"name": "TorrPreload", "author": "x"}
        ];

        var plugins_push = []

        plugins_add.forEach(function(plugin){
            if(!plugins.find(function(a){ return a.url == plugin.url})){
                Lampa.Plugins.add(plugin);
                Lampa.Plugins.save();

                plugins_push.push(plugin.url)
            }
        });

        if(plugins_push.length) Lampa.Utils.putScript(plugins_push,function(){},function(){},function(){},true);

        /*
        setTimeout(function(){
            Lampa.Noty.show('Плагины установлены, перезагрузка через 5 секунд.',{time: 5000})
        },5000)
        */
    }
})();
