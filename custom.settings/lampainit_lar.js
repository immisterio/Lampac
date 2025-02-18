(function() {
    'use strict';
  
    localStorage.setItem('cub_mirrors', '["mirror-kurwa.men", "cub.rip"]');
    
    window.lampa_settings = {
      torrents_use: true,
      demo: false,
      read_only: false,
      socket_use: true,
      account_use: true,
      account_sync: true,
      plugins_store: true,
      feed: true,
      white_use: true,
      push_state: true,
      lang_use: true,
      plugins_use: true
    }
  
    window.lampa_settings.disable_features = {
      dmca: true,
      reactions: false,
      discuss: false,
      ai: false,
      install_proxy: false,
      subscribe: false,
      blacklist: false,
      persons: false
    }
    
    {lampainit-invc}
  
    var timer = setInterval(function() {
      if (typeof Lampa !== 'undefined') {
        clearInterval(timer);
        
        if (window.lampainit_invc)
          window.lampainit_invc.appload();
  
        if ({btn_priority_forced})
          Lampa.Storage.set('full_btn_priority', '{full_btn_priority_hash}');
  
        var unic_id = Lampa.Storage.get('lampac_unic_id', '');
        if (!unic_id) {
          unic_id = Lampa.Utils.uid(8).toLowerCase();
          Lampa.Storage.set('lampac_unic_id', unic_id);
        }
  
        Lampa.Utils.putScriptAsync(["{localhost}/cubproxy.js", "{localhost}/privateinit.js?account_email=" + encodeURIComponent(Lampa.Storage.get('account_email', '')) + "&uid=" + encodeURIComponent(Lampa.Storage.get('lampac_unic_id', ''))], function() {});
  
        if (!Lampa.Storage.get('lampac_initiale', 'false')) {
          if (window.appready) {
            if (window.lampainit_invc) window.lampainit_invc.appready();
            start();
          }
          else {
            Lampa.Listener.follow('app', function(e) {
              if (e.type == 'ready') {
                if (window.lampainit_invc) window.lampainit_invc.appready();
                start();
              }
            })
          }
        }
  
        {deny} 
        {pirate_store}
      }
    }, 200);
  
    function start() {		
      Lampa.Storage.set('lampac_initiale','true');
      Lampa.Storage.set('source','cub');
      Lampa.Storage.set('full_btn_priority','{full_btn_priority_hash}');
      Lampa.Storage.set('proxy_tmdb','{country}'=='RU');
      Lampa.Storage.set('poster_size','w500');
  
      Lampa.Storage.set('parser_use','true'); // использовать парсер
      Lampa.Storage.set('jackett_url','{jachost}');
      Lampa.Storage.set('jackett_key','1');
      Lampa.Storage.set('parser_torrent_type','jackett');
      Lampa.Storage.set('torrserver_use_link','one'); // основной адрес TS
      Lampa.Storage.set('torrserver_url','192.168.10.140:8090'); // LAR IP
      Lampa.Storage.set('torrserver_url_two','192.168.3.240:8090'); // UVA IP
      Lampa.Storage.set('torrserver_auth','false');
      Lampa.Storage.set('internal_torrclient','false');
      Lampa.Storage.set('background_type','complex'); // Сложный задник
      Lampa.Storage.set('video_quality_default','2160'); // 4К по-умолчанию
      Lampa.Storage.set('Reloadbutton','true'); // Кнопка перезагрузки
      Lampa.Storage.set('screensaver','false'); // Выкл скринсейвера
      Lampa.Storage.set('account_use','true');
      Lampa.Storage.set('torrserver_preload','true'); // Использовать прелоадер TS
      Lampa.Storage.set('proxy_tmdb','true'); // TMDB proxy
      Lampa.Storage.set('full_btn_priority','1329165215'); // Torrent button priority

      /*
      Lampa.Storage.set('menu_sort','["Главная","Фильмы","Сериалы","Избранное","История"]');
      Lampa.Storage.set('menu_hide','["Лента","Персона","Аниме","Релизы"]');
      Lampa.Storage.set('torrserver_url_two','192.168.10.140:8090');
      */
  
      var plugins = Lampa.Plugins.get();
  
      var plugins_add = [
          {initiale},
          {"url": "{localhost}/plugins/nc.js", "status": 1,"name": "NewCategories", "author": "x"}, // Плагин доп категорий
          {"url": "{localhost}/plugins/mult.js", "status": 1,"name": "Mult", "author": "x"}, // Плагин переименования Аниме в Мультфильмы
          {"url": "{localhost}/plugins/pubtorr.js", "status": 1,"name": "PublicParsers", "author": "x"}, // Плагин публичных парсеров
          {"url": "{localhost}/plugins/ts-preload.js", "status": 1,"name": "TorrPreload", "author": "x"}, // Плагин предзагрузки TS
          {"url": "{localhost}/backup.js", "status": 1,"name": "Backup", "author": "x"} // Плагин резервного копирования
  //            {"url": "{localhost}/pirate_store.js", "status": 1,"name": "PirateStore", "author": "x"} // Плагин пиратского стора
      ];
  
      var plugins_push = []
  
      plugins_add.forEach(function(plugin) {
        if (!plugins.find(function(a) {
            return a.url == plugin.url
          })) {
          Lampa.Plugins.add(plugin);
          Lampa.Plugins.save();
  
          plugins_push.push(plugin.url)
        }
      });
  
      if (plugins_push.length) Lampa.Utils.putScript(plugins_push, function() {}, function() {}, function() {}, true);
      
      if (window.lampainit_invc)
        window.lampainit_invc.first_initiale();
  
      /*
      setTimeout(function(){
          Lampa.Noty.show('Плагины установлены, перезагрузка через 5 секунд.',{time: 5000})
      },5000)
      */
    }
  })();