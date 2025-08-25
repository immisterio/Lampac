using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Playwright;
using Shared.PlaywrightCore;
using YamlDotNet.Serialization;
using Shared.Models.SISI.NextHUB;

namespace SISI.Controllers.NextHUB
{
    public static class Root
    {
        #region evalOptionsFull
        public static ScriptOptions evalOptionsFull = ScriptOptions.Default

            .AddReferences(typeof(Playwright).Assembly)
            .AddImports(typeof(Playwright).Namespace)

            .AddReferences(typeof(PlaywrightBrowser).Assembly)
            .AddImports(typeof(PlaywrightBrowser).Namespace)

            .AddReferences(typeof(Newtonsoft.Json.JsonConvert).Assembly)
            .AddImports("Newtonsoft.Json")
            .AddImports("Newtonsoft.Json.Linq")

            .AddReferences(typeof(RchClient).Assembly)
            .AddReferences(typeof(Http).Assembly)
            .AddImports("Lampac.Engine.CORE")

            .AddReferences(typeof(PlaylistItem).Assembly)
            .AddImports(typeof(PlaylistItem).Namespace)
            .AddImports("Shared.Model.SISI");
        #endregion

        public static NxtSettings goInit(string plugin)
        {
            if (string.IsNullOrEmpty(plugin))
                return null;

            if (AppInit.conf.sisi.NextHUB_sites_enabled != null && !AppInit.conf.sisi.NextHUB_sites_enabled.Contains(plugin))
                return null;

            if (!File.Exists($"NextHUB/sites/{plugin}.yaml"))
                return null;

            var hybridCache = new HybridCache();

            string memKey = $"NextHUB:goInit:{plugin}";
            if (!hybridCache.TryGetValue(memKey, out NxtSettings init))
            {
                var deserializer = new DeserializerBuilder().Build();

                // Чтение основного YAML-файла
                string yaml = File.ReadAllText($"NextHUB/sites/{plugin}.yaml");
                var target = deserializer.Deserialize<Dictionary<object, object>>(yaml);

                if (File.Exists($"NextHUB/override/{plugin}.yaml") || File.Exists($"NextHUB/override/_.yaml"))
                {
                    // Чтение пользовательского YAML-файла
                    string myYaml = File.Exists($"NextHUB/override/{plugin}.yaml") ? File.ReadAllText($"NextHUB/override/{plugin}.yaml") : File.ReadAllText("NextHUB/override/_.yaml");
                    var mySource = deserializer.Deserialize<Dictionary<object, object>>(myYaml);

                    // Объединение словарей
                    foreach (var property in mySource)
                    {
                        if (!target.ContainsKey(property.Key))
                        {
                            target[property.Key] = property.Value;
                            continue;
                        }

                        if (property.Value is IDictionary<object, object> sourceDict &&
                            target[property.Key] is IDictionary<object, object> targetDict)
                        {
                            // Рекурсивное объединение вложенных словарей
                            foreach (var item in sourceDict)
                                targetDict[item.Key] = item.Value;
                        }
                        else
                        {
                            target[property.Key] = property.Value;
                        }
                    }
                }

                // Преобразование словаря в объект NxtSettings
                var serializer = new SerializerBuilder().Build();

                var yamlResult = serializer.Serialize(target);
                init = deserializer.Deserialize<NxtSettings>(yamlResult);

                if (string.IsNullOrEmpty(init.plugin))
                    init.plugin = init.displayname;

                if (!init.debug || !AppInit.conf.multiaccess)
                    hybridCache.Set(memKey, init, DateTime.Now.AddMinutes(1), inmemory: true);
            }

            return init;
        }
    }
}
