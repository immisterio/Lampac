
var sync_invc = {
  import_keys: [''] // 'myfavorite', 'profiles'
};


sync_invc.goExport = function goExport(path, value) {
  // можно добавить свои поля или изменить стандартные в синхронизации
  return value;
};

sync_invc.importСompleted  = function importСompleted(path) {
  // импорт завершён, при необходимости можно выполнить дополнительный код
};


// Вызвать export c path 'myfavorite'
// window.lwsEvent.send('sync', 'myfavorite');

// Вызвать export для закладок и просмотров
//window.lwsEvent.send('sync', 'sync_favorite');
//window.lwsEvent.send('sync', 'sync_view');

// Отправить событие по socket_id
// window.lwsEvent.sendId(connectionId, 'openlink', 'json');
