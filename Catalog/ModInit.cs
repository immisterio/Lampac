using YamlDotNet.Serialization;

namespace Catalog
{
    public class ModInit
    {
        public static void loaded()
        {
        }

        #region goInit
        public static CatalogSettings goInit(string site)
        {
            if (string.IsNullOrEmpty(site))
                return null;

            site = site.Trim().ToLowerInvariant();

            if (!File.Exists($"catalog/sites/{site}.yaml"))
                return null;

            var hybridCache = new HybridCache();

            string memKey = $"catalog:goInit:{site}";
            if (!hybridCache.TryGetValue(memKey, out CatalogSettings init))
            {
                var deserializer = new DeserializerBuilder().Build();

                // Чтение основного YAML-файла
                string yaml = File.ReadAllText($"catalog/sites/{site}.yaml");
                var target = deserializer.Deserialize<Dictionary<object, object>>(yaml);

                foreach (string y in new string[] { "_", site })
                {
                    if (File.Exists($"catalog/override/{y}.yaml"))
                    {
                        // Чтение пользовательского YAML-файла
                        string myYaml = File.ReadAllText($"catalog/override/{y}.yaml");
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
                }

                // Преобразование словаря в объект CatalogSettings
                var serializer = new SerializerBuilder().Build();

                var yamlResult = serializer.Serialize(target);
                init = deserializer.Deserialize<CatalogSettings>(yamlResult);

                if (string.IsNullOrEmpty(init.plugin))
                    init.plugin = init.displayname;

                if (!init.debug || !AppInit.conf.multiaccess)
                    hybridCache.Set(memKey, init, DateTime.Now.AddMinutes(1), inmemory: true);
            }

            return init;
        }
        #endregion

        #region IsRhubFallback
        public static bool IsRhubFallback(BaseSettings init)
        {
            if (init.rhub && init.rhub_fallback)
            {
                init.rhub = false;
                return true;
            }

            return false;
        }
        #endregion

        #region nodeValue - HtmlNode
        public static object nodeValue(HtmlNode node, SingleNodeSettings nd, string host)
        {
            string value = null;

            if (nd != null)
            {
                if (string.IsNullOrEmpty(nd.node) && (!string.IsNullOrEmpty(nd.attribute) || nd.attributes != null))
                {
                    if (nd.attributes != null)
                    {
                        foreach (var attr in nd.attributes)
                        {
                            var attrValue = node.GetAttributeValue(attr, null);
                            if (!string.IsNullOrEmpty(attrValue))
                            {
                                value = attrValue;
                                break;
                            }
                        }
                    }
                    else
                    {
                        value = node.GetAttributeValue(nd.attribute, null);
                    }
                }
                else
                {
                    var inNode = node.SelectSingleNode(nd.node);
                    if (inNode != null)
                    {
                        if (nd.attributes != null)
                        {
                            foreach (var attr in nd.attributes)
                            {
                                var attrValue = inNode.GetAttributeValue(attr, null);
                                if (!string.IsNullOrEmpty(attrValue))
                                {
                                    value = attrValue;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            value = (!string.IsNullOrEmpty(nd.attribute) ? inNode.GetAttributeValue(nd.attribute, null) : inNode.InnerText)?.Trim();
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(value))
                return null;

            if (nd.format != null)
            {
                var options = ScriptOptions.Default
                    .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll"))
                    .AddImports("Shared")
                    .AddImports("Shared.Engine")
                    .AddImports("Shared.Models")
                    .AddReferences(CSharpEval.ReferenceFromFile("Newtonsoft.Json.dll"))
                    .AddImports("Newtonsoft.Json")
                    .AddImports("Newtonsoft.Json.Linq");

                return CSharpEval.Execute<object>(nd.format, new CatalogNodeValue(value, host), options);
            }

            return value?.Trim();
        }
        #endregion

        #region nodeValue - JToken
        public static object nodeValue(JToken node, SingleNodeSettings nd, string host)
        {
            if (node == null || nd == null)
                return null;

            var current = node is JProperty property ? property.Value : node;

            JToken valueToken = null;

            if (!string.IsNullOrEmpty(nd.node))
            {
                valueToken = current.SelectToken(nd.node);
            }
            else
            {
                if (nd.attributes != null)
                {
                    foreach (var attr in nd.attributes)
                    {
                        valueToken = current[attr];
                        if (valueToken != null)
                            break;
                    }
                }

                if (valueToken == null && !string.IsNullOrEmpty(nd.attribute))
                    valueToken = current[nd.attribute];
            }

            if (valueToken == null)
                return null;

            string value = valueToken switch
            {
                JValue jValue => jValue.Value?.ToString(),
                JProperty jProp => jProp.Value?.ToString(),
                _ => valueToken.ToString(Formatting.None)
            };

            if (string.IsNullOrEmpty(value))
                return null;

            if (nd.format != null)
            {
                var options = ScriptOptions.Default
                    .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll"))
                    .AddImports("Shared")
                    .AddImports("Shared.Engine")
                    .AddImports("Shared.Models")
                    .AddReferences(CSharpEval.ReferenceFromFile("Newtonsoft.Json.dll"))
                    .AddImports("Newtonsoft.Json")
                    .AddImports("Newtonsoft.Json.Linq");

                return CSharpEval.Execute<object>(nd.format, new CatalogNodeValue(value, host), options);
            }

            if (valueToken is JValue)
                return value?.Trim();

            return valueToken;
        }
        #endregion
    }
}
