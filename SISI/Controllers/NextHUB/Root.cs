using Shared.Engine.CORE;
using Shared.Model.SISI.NextHUB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;

namespace Lampac.Controllers.NextHUB
{
    public static class Root
    {
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

                if (File.Exists($"NextHUB/override/{plugin}.yaml"))
                {
                    // Чтение пользовательского YAML-файла
                    string myYaml = File.ReadAllText($"NextHUB/override/{plugin}.yaml");
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
                    hybridCache.Set(memKey, init, DateTime.Now.AddMinutes(1));
            }

            return init;
        }
    }
}
