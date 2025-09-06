using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Scripting;
using Shared.Engine;
using Shared.Models.Base;
using Shared.Models.Events;
using System.Net;
using System.Net.Http;
using System.Threading;
using YamlDotNet.Serialization;

namespace Shared
{
    public class InvkEvent
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
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

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
                    if (Path.GetFileName(file) == "example.yaml")
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


        public static void LoadKit(EventLoadKit model) => Invoke(conf?.LoadKit, model);

        #region BadInitialization
        public static Task<ActionResult> BadInitialization(EventBadInitialization model)
        {
            var option = ScriptOptions.Default.AddReferences(typeof(ActionResult).Assembly).AddImports("Microsoft.AspNetCore.Mvc")
                                              .AddReferences(typeof(BaseSettings).Assembly).AddImports("Shared.Models.Base");

            return InvokeAsync<ActionResult>(conf?.Controller?.BadInitialization, model, option);
        }
        #endregion

        #region Middleware
        public static Task<bool> Middleware(bool first, EventMiddleware model)
        {
            var option = ScriptOptions.Default.AddReferences(typeof(HttpContext).Assembly).AddImports("Microsoft.AspNetCore.Http")
                                              .AddReferences(typeof(Task).Assembly).AddImports("System.Threading.Tasks")
                                              .AddReferences(typeof(TimeSpan).Assembly).AddImports("System");

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

            return Invoke<string>(code, model);
        }
        #endregion

        #region Http
        public static void Http(string e, object model)
        {
            string code = null;

            switch (e)
            {
                case "handler":
                    code = conf?.Http?.Handler;
                    break;

                case "headers":
                    code = conf?.Http?.Headers;
                    break;

                case "response":
                    code = conf?.Http?.Response;
                    break;
            }

            var option = ScriptOptions.Default.AddReferences(typeof(WebProxy).Assembly).AddImports("System.Net")
                                              .AddReferences(typeof(HttpClientHandler).Assembly).AddImports("System.Net.Http");

            Invoke(code, model, option);
        }
        #endregion
    }
}
