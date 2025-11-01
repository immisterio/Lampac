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

            var hybridCache = new HybridCache();

            string memKey = $"catalog:goInit:{site}";
            if (!hybridCache.TryGetValue(memKey, out CatalogSettings init))
            {
                // Если файл не найден по имени, пробуем найти по displayname в *.yaml
                if (!File.Exists($"catalog/sites/{site}.yaml"))
                {
                    string found = FindSiteByDisplayName(site);
                    if (string.IsNullOrEmpty(found))
                        return null;

                    site = found;
                }

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

        #region FindSiteByDisplayName
        static string FindSiteByDisplayName(string site)
        {
            var deserializer = new DeserializerBuilder().Build();

            foreach (var folder in new[] { "catalog/sites", "catalog/override" })
            {
                if (!Directory.Exists(folder))
                    continue;

                foreach (var file in Directory.EnumerateFiles(folder, "*.yaml"))
                {
                    try
                    {
                        var yaml = File.ReadAllText(file);
                        var dict = deserializer.Deserialize<Dictionary<object, object>>(yaml);
                        if (dict != null && dict.TryGetValue("displayname", out var dnObj) && dnObj != null)
                        {
                            var dn = dnObj.ToString().Trim().ToLowerInvariant();
                            if (dn == site)
                                return Path.GetFileNameWithoutExtension(file);
                        }
                    }
                    catch { }
                }
            }

            return null;
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
                current = current.SelectToken(nd.node);
                if (current == null)
                    return null;
            }

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


        #region setArgsValue
        public static void setArgsValue(SingleNodeSettings arg, object val, JObject jo)
        {
            if (val != null)
            {
                if (arg.name_arg is "kp_rating" or "imdb_rating")
                {
                    string rating = val?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(rating) && rating != "0" && rating != "0.0" && double.TryParse(rating.Replace(".", ","), out _))
                    {
                        rating = rating.Length > 3 ? rating.Substring(0, 3) : rating;
                        if (rating.Length == 1)
                            rating = $"{rating}.0";

                        jo[arg.name_arg] = JToken.FromObject(rating.Replace(",", "."));
                    }
                }
                else if (arg.name_arg is "vote_average")
                {
                    string value = val?.ToString()?.Trim()?.Replace(".", ",");
                    if (!string.IsNullOrEmpty(value) && double.TryParse(value, out double _v) && _v > 0)
                        jo[arg.name_arg] = JToken.FromObject(_v);
                }
                else if (arg.name_arg is "runtime" or "PG")
                {
                    string value = val?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(value) && long.TryParse(value, out long _v) && _v > 0)
                        jo[arg.name_arg] = JToken.FromObject(_v);
                }
                else if (arg.name_arg is "genres" or "created_by" or "production_countries" or "production_companies" or "networks" or "spoken_languages")
                {
                    if (val is string)
                    {
                        string arrayStr = val?.ToString();
                        var array = new JArray();

                        if (!string.IsNullOrEmpty(arrayStr))
                        {
                            foreach (string str in arrayStr.Split(","))
                            {
                                if (string.IsNullOrWhiteSpace(str))
                                    continue;

                                array.Add(new JObject() { ["name"] = clearText(str) });
                            }

                            jo[arg.name_arg] = array;
                        }
                    }
                    else if (IsStringList(val as JToken))
                    {
                        var array = new JArray();
                        foreach (var item in (JArray)val)
                            array.Add(new JObject() { ["name"] = clearText(item.ToString()) });

                        jo[arg.name_arg] = array;
                    }
                }
                else if (val is string && (arg.name_arg is "origin_country" or "languages"))
                {
                    string arrayStr = val?.ToString();
                    var array = new JArray();

                    if (!string.IsNullOrEmpty(arrayStr))
                    {
                        foreach (string str in arrayStr.Split(","))
                        {
                            if (!string.IsNullOrWhiteSpace(str))
                                array.Add(str.Trim());
                        }

                        if (array.Count > 0)
                            jo[arg.name_arg] = array;
                    }
                }
                else
                {
                    jo[arg.name_arg] = JToken.FromObject(val);
                }
            }
        }
        #endregion

        #region IsStringList
        static bool IsStringList(JToken token)
        {
            if (token?.Type != JTokenType.Array)
                return false;

            var array = token as JArray;
            return array?.All(item => item.Type == JTokenType.String) == true;
        }
        #endregion

        #region clearText
        public static string clearText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = text.Replace("&nbsp;", "");
            text = Regex.Replace(text, "<[^>]+>", "");
            text = HttpUtility.HtmlDecode(text);
            return text.Trim();
        }
        #endregion
    }
}
