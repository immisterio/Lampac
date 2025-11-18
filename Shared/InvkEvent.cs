using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Scripting;
using Shared.Engine;
using Shared.Models;
using Shared.Models.Events;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using YamlDotNet.Serialization;

namespace Shared
{
    public static class InvkEvent
    {
        #region static
        static InvkEvent()
        {
            updateConf();

            var eventsDir = Path.Combine(AppContext.BaseDirectory, "events");
            var lastWriteTimes = new Dictionary<string, DateTime>();

            // Инициализация дат
            foreach (var file in Directory.Exists(eventsDir) ? Directory.GetFiles(eventsDir, "*.yaml") : Array.Empty<string>())
                lastWriteTimes[file] = File.GetLastWriteTimeUtc(file);

            ThreadPool.QueueUserWorkItem(async _ =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));

                    if (!Directory.Exists(eventsDir))
                        continue;

                    bool changed = false;
                    var files = Directory.GetFiles(eventsDir, "*.yaml");

                    // Проверка новых и изменённых файлов
                    foreach (var file in files)
                    {
                        var writeTime = File.GetLastWriteTimeUtc(file);
                        if (!lastWriteTimes.TryGetValue(file, out var lastTime) || writeTime != lastTime)
                        {
                            changed = true;
                            lastWriteTimes[file] = writeTime;
                        }
                    }

                    // Проверка удалённых файлов
                    foreach (var file in lastWriteTimes.Keys.ToList())
                    {
                        if (!files.Contains(file))
                        {
                            changed = true;
                            lastWriteTimes.Remove(file);
                        }
                    }

                    if (changed)
                        updateConf();
                }
            });
        }
        #endregion

        #region conf
        public static EventsModel conf = new EventsModel();

        static void updateConf()
        {
            var eventsDir = Path.Combine(AppContext.BaseDirectory, "events");
            if (!Directory.Exists(eventsDir))
                return;

            var deserializer = new DeserializerBuilder().Build();
            var serializer = new SerializerBuilder().Build();

            // Итоговый словарь для объединения всех файлов
            var merged = new Dictionary<object, object>();

            foreach (string file in Directory.GetFiles(eventsDir, "*.yaml"))
            {
                try
                {
                    if (Path.GetFileName(file) is "example.yaml" or "interceptors.yaml")
                        continue;

                    var yaml = File.ReadAllText(file);
                    var dict = deserializer.Deserialize<Dictionary<object, object>>(yaml);

                    foreach (var property in dict)
                    {
                        if (!merged.ContainsKey(property.Key))
                        {
                            merged[property.Key] = property.Value;
                            continue;
                        }

                        if (property.Value is IDictionary<object, object> sourceDict &&
                            merged[property.Key] is IDictionary<object, object> targetDict)
                        {
                            foreach (var item in sourceDict)
                                targetDict[item.Key] = item.Value;
                        }
                        else
                        {
                            merged[property.Key] = property.Value;
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex); }
            }

            // Преобразуем объединённый словарь обратно в YAML, затем десериализуем в EventsModel
            var yamlResult = serializer.Serialize(merged);
            conf = deserializer.Deserialize<EventsModel>(yamlResult);
        }
        #endregion

        #region FileOrCode
        static string FileOrCode(string _val)
        {
            if (_val.EndsWith(".cs"))
                return FileCache.ReadAllText(Path.Combine("events", _val));

            return _val;
        }
        #endregion


        #region Invoke<T>
        static T Invoke<T>(string cs, object model, ScriptOptions options = null)
        {
            try
            {
                if (cs != null)
                    return CSharpEval.Execute<T>(FileOrCode(cs), model, options);
            }
            catch { }

            return default;
        }
        #endregion

        #region InvokeAsync<T>
        static Task<T> InvokeAsync<T>(string cs, object model, ScriptOptions options = null)
        {
            try
            {
                if (cs != null)
                    return CSharpEval.ExecuteAsync<T>(FileOrCode(cs), model, options);
            }
            catch { }

            return Task.FromResult(default(T));
        }
        #endregion

        #region Invoke
        static void Invoke(string cs, object model, ScriptOptions options = null)
        {
            try
            {
                if (cs != null)
                    CSharpEval.Execute(FileOrCode(cs), model, options);
            }
            catch { }
        }
        #endregion

        #region InvokeAsync
        static Task InvokeAsync(string cs, object model, ScriptOptions options = null)
        {
            try
            {
                if (cs != null)
                    return CSharpEval.ExecuteAsync(FileOrCode(cs), model, options);
            }
            catch { }

            return Task.CompletedTask;
        }
        #endregion


        #region LoadKit
        public static void LoadKit(EventLoadKit model)
        {
            EventListener.LoadKit?.Invoke(model);

            if (conf?.LoadKit == null)
                return;

            Invoke(conf?.LoadKit, model, ScriptOptions.Default
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared.Models.Base")
                .AddReferences(typeof(File).Assembly).AddImports("System.IO"));
        }
        #endregion

        #region ProxyApi
        async public static Task ProxyApi(object model)
        {
            string code = null;

            if (model is EventProxyApiCreateHttpRequest httpRequestModel)
            {
                code = conf?.ProxyApi?.CreateHttpRequest;

                if (EventListener.ProxyApiCreateHttpRequest != null)
                    await EventListener.ProxyApiCreateHttpRequest.Invoke(httpRequestModel);
            }

            if (string.IsNullOrEmpty(code))
                return;

            var option = ScriptOptions.Default
                .AddReferences(typeof(HttpRequest).Assembly).AddImports("Microsoft.AspNetCore.Http")
                .AddReferences(typeof(Task).Assembly).AddImports("System.Threading.Tasks")
                .AddReferences(CSharpEval.ReferenceFromFile("Newtonsoft.Json.dll")).AddImports("Newtonsoft.Json").AddImports("Newtonsoft.Json.Linq")
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared").AddImports("Shared.Models").AddImports("Shared.Engine")
                .AddReferences(typeof(HttpRequestMessage).Assembly).AddImports("System.Net.Http")
                .AddReferences(typeof(System.Net.Cookie).Assembly).AddImports("System.Net")
                .AddReferences(typeof(File).Assembly).AddImports("System.IO");

            await InvokeAsync(code, model, option);
        }
        #endregion

        #region BadInitialization
        public static Task<ActionResult> BadInitialization(EventBadInitialization model)
        {
            if (conf?.Controller?.BadInitialization == null)
            {
                if (EventListener.BadInitialization != null)
                    return EventListener.BadInitialization.Invoke(model);

                return Task.FromResult(default(ActionResult));
            }

            var option = ScriptOptions.Default
                .AddReferences(typeof(ActionResult).Assembly).AddImports("Microsoft.AspNetCore.Mvc")
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared.Models.Base").AddImports("Shared.Engine")
                .AddReferences(CSharpEval.ReferenceFromFile("Newtonsoft.Json.dll")).AddImports("Newtonsoft.Json").AddImports("Newtonsoft.Json.Linq")
                .AddReferences(typeof(File).Assembly).AddImports("System.IO");

            return InvokeAsync<ActionResult>(conf?.Controller?.BadInitialization, model, option);
        }
        #endregion

        #region HostStreamProxy
        public static string HostStreamProxy(EventHostStreamProxy model)
        {
            if (conf?.Controller?.HostStreamProxy == null)
                return EventListener.HostStreamProxy?.Invoke(model);

            var option = ScriptOptions.Default
                .AddReferences(typeof(HttpContext).Assembly).AddImports("Microsoft.AspNetCore.Http")
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared.Engine").AddImports("Shared.Models.Base").AddImports("Shared.Models")
                .AddReferences(typeof(WebProxy).Assembly).AddImports("System.Net")
                .AddReferences(typeof(MD5).Assembly).AddImports("System.Security.Cryptography")
                .AddImports("System.Collections.Generic");

            return Invoke<string>(conf.Controller.HostStreamProxy, model, option);
        }
        #endregion

        #region MyLocalIp
        public static Task<string> MyLocalIp(EventMyLocalIp model)
        {
            if (string.IsNullOrEmpty(conf?.Controller?.MyLocalIp))
            {
                if (EventListener.MyLocalIp != null)
                    return EventListener.MyLocalIp.Invoke(model);

                return Task.FromResult(default(string));
            }

            var option = ScriptOptions.Default
                .AddReferences(typeof(HttpContext).Assembly).AddImports("Microsoft.AspNetCore.Http")
                .AddReferences(CSharpEval.ReferenceFromFile("Newtonsoft.Json.dll")).AddImports("Newtonsoft.Json").AddImports("Newtonsoft.Json.Linq")
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared.Engine").AddImports("Shared.Models.Base")
                .AddReferences(typeof(File).Assembly).AddImports("System.IO");

            return InvokeAsync<string>(conf.Controller.MyLocalIp, model, option);
        }
        #endregion

        #region HttpHeaders
        public static List<HeadersModel> HttpHeaders(EventControllerHttpHeaders model)
        {
            if (string.IsNullOrEmpty(conf?.Controller?.HttpHeaders))
                return EventListener.HttpHeaders?.Invoke(model);

            var option = ScriptOptions.Default
                .AddReferences(typeof(HttpContext).Assembly).AddImports("Microsoft.AspNetCore.Http")
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared.Models.Base").AddImports("Shared.Models")
                .AddReferences(typeof(File).Assembly).AddImports("System.IO")
                .AddImports("System.Collections.Generic");

            return Invoke<List<HeadersModel>>(conf.Controller.HttpHeaders, model, option);
        }
        #endregion

        #region Middleware
        public static Task<bool> Middleware(bool first, EventMiddleware model)
        {
            if ((first ? conf?.Middleware?.first : conf?.Middleware?.end) == null)
            {
                if (EventListener.Middleware != null)
                    return EventListener.Middleware.Invoke(first, model);

                return Task.FromResult(default(bool));
            }

            var option = ScriptOptions.Default
                .AddReferences(typeof(HttpContext).Assembly).AddImports("Microsoft.AspNetCore.Http")
                .AddReferences(typeof(Task).Assembly).AddImports("System.Threading.Tasks")
                .AddReferences(CSharpEval.ReferenceFromFile("Newtonsoft.Json.dll")).AddImports("Newtonsoft.Json").AddImports("Newtonsoft.Json.Linq")
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared").AddImports("Shared.Models").AddImports("Shared.Engine")
                .AddReferences(typeof(HttpRequestMessage).Assembly).AddImports("System.Net.Http")
                .AddReferences(typeof(System.Net.Cookie).Assembly).AddImports("System.Net")
                .AddReferences(typeof(File).Assembly).AddImports("System.IO");

            return InvokeAsync<bool>(first ? conf?.Middleware?.first : conf?.Middleware?.end, model, option);
        }
        #endregion

        #region AppReplace
        public static string AppReplace(string e, EventAppReplace model)
        {
            string code = null;

            switch (e)
            {
                case "online":
                    code = conf?.Controller?.AppReplace?.online?.eval;
                    break;

                case "sisi":
                    code = conf?.Controller?.AppReplace?.sisi?.eval;
                    break;

                case "appjs":
                    code = conf?.Controller?.AppReplace?.appjs?.eval;
                    break;

                case "appcss":
                    code = conf?.Controller?.AppReplace?.appcss?.eval;
                    break;
            }

            if (string.IsNullOrEmpty(code))
                return EventListener.AppReplace?.Invoke(e, model);

            return Invoke<string>(code, model, ScriptOptions.Default.AddReferences(typeof(File).Assembly).AddImports("System.IO"));
        }
        #endregion

        #region Http
        public static void Http(object model)
        {
            string code = null;
            var modelType = model.GetType();

            if (modelType == typeof(EventHttpHandler))
            {
                code = conf?.Http?.Handler;
                EventListener.HttpHandler?.Invoke((EventHttpHandler)model);
            }

            else if (modelType == typeof(EventHttpHeaders))
            {
                code = conf?.Http?.Headers;
                EventListener.HttpRequestHeaders?.Invoke((EventHttpHeaders)model);
            }

            if (string.IsNullOrEmpty(code))
                return;

            var option = ScriptOptions.Default
                .AddReferences(typeof(WebProxy).Assembly).AddImports("System.Net")
                .AddReferences(typeof(HttpClientHandler).Assembly).AddImports("System.Net.Http")
                .AddReferences(typeof(File).Assembly).AddImports("System.IO");

            Invoke(code, model, option);
        }

        public static Task HttpAsync(object model)
        {
            string code = null;
            var modelType = model.GetType();

            if (modelType == typeof(EventHttpResponse))
            {
                code = conf?.Http?.Response;

                if (string.IsNullOrEmpty(code) && EventListener.HttpResponse != null)
                    return EventListener.HttpResponse.Invoke((EventHttpResponse)model);
            }

            if (string.IsNullOrEmpty(code))
                return Task.CompletedTask;

            var option = ScriptOptions.Default
                .AddReferences(typeof(WebProxy).Assembly).AddImports("System.Net")
                .AddReferences(typeof(HttpClientHandler).Assembly).AddImports("System.Net.Http");

            if (modelType == typeof(EventHttpResponse))
            {
                option.AddReferences(CSharpEval.ReferenceFromFile("Newtonsoft.Json.dll")).AddImports("Newtonsoft.Json").AddImports("Newtonsoft.Json.Linq")
                      .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared").AddImports("Shared.Models").AddImports("Shared.Engine")
                      .AddReferences(typeof(File).Assembly).AddImports("System.IO");
            }

            return InvokeAsync(code, model, option);
        }
        #endregion

        #region RedApi
        public static bool RedApi(string e, object model)
        {
            string code = null;

            switch (e)
            {
                case "addtorrent":
                    code = conf?.RedApi?.AddTorrents;
                    break;
            }

            return Invoke<bool>(code, model);
        }
        #endregion

        #region Externalids
        public static void Externalids(string id, ref string imdb_id, ref string kinopoisk_id, int serial)
        {
            (string imdb_id, string kinopoisk_id) result = default;

            var md = new EventExternalids(id, imdb_id, kinopoisk_id, serial);

            if (EventListener.Externalids != null)
                result = EventListener.Externalids.Invoke(md);

            if (!string.IsNullOrEmpty(conf?.Controller?.Externalids))
                result = Invoke<(string imdb_id, string kinopoisk_id)>(conf.Controller.Externalids, md);

            if (result == default || (result.imdb_id == null && result.kinopoisk_id == null))
                return;

            imdb_id = result.imdb_id;
            kinopoisk_id = result.kinopoisk_id;
        }
        #endregion

        #region StreamQualityTpl
        public static (bool? next, string link) StreamQuality(EventStreamQuality model)
        {
            if (string.IsNullOrEmpty(conf?.StreamQualityTpl))
                return EventListener.StreamQuality?.Invoke(model) ?? default;

            var option = ScriptOptions.Default
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared.Models.Events").AddImports("Shared.Models.Templates");

            return Invoke<(bool? next, string link)>(conf.StreamQualityTpl, model, option);
        }

        public static (string link, string quality)? StreamQualityFirts(EventStreamQualityFirts model)
        {
            if (string.IsNullOrEmpty(conf?.StreamQualityFirts))
                return EventListener.StreamQualityFirts?.Invoke(model);

            var option = ScriptOptions.Default
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared.Models.Events").AddImports("Shared.Models.Templates");

            return Invoke<(string link, string quality)?>(conf.StreamQualityFirts, model, option);
        }
        #endregion

        #region Transcoding
        public static void Transcoding(EventTranscoding model)
        {
            EventListener.TranscodingCreateProcess?.Invoke(model);

            var code = conf?.Transcoding?.CreateProcess;
            if (string.IsNullOrEmpty(code))
                return;

            var option = ScriptOptions.Default
                .AddReferences(typeof(Collection<string>).Assembly).AddImports("System.Collections.ObjectModel")
                .AddReferences(CSharpEval.ReferenceFromFile("Newtonsoft.Json.dll")).AddImports("Newtonsoft.Json").AddImports("Newtonsoft.Json.Linq")
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared").AddImports("Shared.Models").AddImports("Shared.Engine").AddImports("Shared.Models.AppConf");

            Invoke(code, model, option);
        }
        #endregion

        #region Rch
        public static void RchRegistry(EventRchRegistry model)
        {
            EventListener.RchRegistry?.Invoke(model);

            var code = conf?.Rch?.Registry;
            if (string.IsNullOrEmpty(code))
                return;

            var option = ScriptOptions.Default
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll"))
                .AddImports("Shared").AddImports("Shared.Models").AddImports("Shared.Engine");

            Invoke(code, model, option);
        }

        public static void RchDisconnected(EventRchDisconnected model)
        {
            EventListener.RchDisconnected?.Invoke(model.connectionId);

            var code = conf?.Rch?.Disconnected;
            if (string.IsNullOrEmpty(code))
                return;

            var option = ScriptOptions.Default
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll"))
                .AddImports("Shared").AddImports("Shared.Models").AddImports("Shared.Engine");

            Invoke(code, model, option);
        }
        #endregion

        #region Nws
        public static void NwsConnected(EventNwsConnected model)
        {
            EventListener.NwsConnected?.Invoke(model);

            var code = conf?.Nws?.Connected;
            if (string.IsNullOrEmpty(code))
                return;

            var option = ScriptOptions.Default
                .AddReferences(typeof(CancellationToken).Assembly).AddImports("System.Threading")
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared").AddImports("Shared.Models").AddImports("Shared.Engine");

            Invoke(code, model, option);
        }

        public static void NwsDisconnected(EventNwsDisconnected model)
        {
            EventListener.NwsDisconnected?.Invoke(model.connectionId);

            var code = conf?.Nws?.Disconnected;
            if (string.IsNullOrEmpty(code))
                return;

            var option = ScriptOptions.Default
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared").AddImports("Shared.Models").AddImports("Shared.Engine");

            Invoke(code, model, option);
        }
        #endregion

        #region HybridCache
        public static (DateTimeOffset ex, string value) HybridCache(string e, string key, string value, DateTimeOffset ex)
        {
            string code = null;

            var model = new EventHybridCache(key, value, ex);

            switch (e)
            {
                case "read":
                    code = conf?.HybridCache?.Read;
                    break;

                case "write":
                    code = conf?.HybridCache?.Write;
                    break;
            }

            if (string.IsNullOrEmpty(code))
                return EventListener.HybridCache?.Invoke(e, model) ?? default;

            var option = ScriptOptions.Default
                .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll")).AddImports("Shared.Engine")
                .AddReferences(CSharpEval.ReferenceFromFile("Newtonsoft.Json.dll")).AddImports("Newtonsoft.Json").AddImports("Newtonsoft.Json.Linq")
                .AddReferences(typeof(File).Assembly).AddImports("System.IO");

            return Invoke<(DateTimeOffset ex, string value)>(code, model, option);
        }
        #endregion

        public static void PidTor(EventPidTor model) => Invoke(conf?.PidTor, model);
    }
}
