// //////////////
// Переименуйте файл lampainit-invc.js в lampainit-invc.my.js
// //////////////


window.lampainit_invc = {};


// Лампа готова для использования 
window.lampainit_invc.appload = function appload() {
  // Lampa.Utils.putScriptAsync(["{localhost}/myplugin.js"]);  // wwwroot/myplugin.js
  // Lampa.Utils.putScriptAsync(["{localhost}/plugins/ts-preload.js", "https://nb557.github.io/plugins/online_mod.js"]);
  // Lampa.Storage.set('proxy_tmdb', 'true');
  // etc
}


// Лампа полностью загружена, можно работать с интерфейсом 
window.lampainit_invc.appready = function appready() {
  // $('.head .notice--icon').remove();
}


// Выполняется один раз, когда пользователь впервые открывает лампу
window.lampainit_invc.first_initiale = function firstinitiale() {
  // Здесь можно указать/изменить первоначальные настройки 
  // Lampa.Storage.set('source', 'tmdb');
}


// Ниже код выполняется до загрузки лампы, например можно изменить настройки 
// window.lampa_settings.push_state = false;
// localStorage.setItem('cub_domain', 'cub.rip');
// localStorage.setItem('cub_mirrors', '["cub.rip", "mirror-kurwa.men", "lampadev.ru"]');
