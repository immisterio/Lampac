using Newtonsoft.Json.Linq;

namespace Shared.Engine
{
    /// <summary>
    /// [Copilot AI]
    /// </summary>
    public static class ModuleInvoke
    {
        public static JObject Conf(string filed, object val)
        {
            if (val == null)
                return null;

            // Convert incoming value to JToken/JObject
            JToken baseToken = val as JToken ?? JToken.FromObject(val);
            if (baseToken == null)
                return null;

            if (baseToken.Type != JTokenType.Object)
            {
                // For non-object values wrap into a simple object so merging still possible
                return new JObject { ["value"] = baseToken };
            }

            var baseObj = (JObject)baseToken;

            try
            {
                if (!File.Exists("init.conf"))
                    return baseObj;

                string initfile = File.ReadAllText("init.conf").Trim();
                if (string.IsNullOrEmpty(initfile))
                    return baseObj;

                if (!initfile.StartsWith("{"))
                    initfile = "{" + initfile + "}";

                JObject jo = null;
                try
                {
                    jo = JObject.Parse(initfile);
                }
                catch
                {
                    // Try to deserialize more leniently
                    try { jo = JObject.FromObject(Newtonsoft.Json.JsonConvert.DeserializeObject(initfile) ?? new JObject()); } catch { jo = null; }
                }

                if (jo == null || !jo.ContainsKey(filed))
                    return baseObj;

                var node = jo[filed];

                // If field explicitly false -> return original val
                if (node.Type == JTokenType.Boolean && node.Value<bool>() == false)
                    return baseObj;

                // If node is not an object, nothing to merge -> return original
                if (node.Type != JTokenType.Object)
                    return baseObj;

                var overrideObj = (JObject)node;

                // Deep clone base
                var result = (JObject)baseObj.DeepClone();

                Merge(result, overrideObj);

                return result;
            }
            catch
            {
                return baseObj;
            }
        }

        static void Merge(JObject target, JObject source)
        {
            foreach (var prop in source.Properties())
            {
                var tprop = target.Property(prop.Name);

                if (tprop != null && tprop.Value.Type == JTokenType.Object && prop.Value.Type == JTokenType.Object)
                {
                    Merge((JObject)tprop.Value, (JObject)prop.Value);
                }
                else
                {
                    // Replace or add
                    target[prop.Name] = prop.Value.DeepClone();
                }
            }
        }
    }
}
