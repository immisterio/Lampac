using Shared.Model.SISI.NextHUB;
using Newtonsoft.Json;
using System.IO;
using Shared.Engine.CORE;
using System;
using Newtonsoft.Json.Linq;
using Z.Expressions;

namespace Lampac.Controllers.NextHUB
{
    public static class Root
    {
        #region goInit
        public static NxtSettings goInit(string plugin)
        {
            if (string.IsNullOrEmpty(plugin) || !File.Exists($"NextHUB/{plugin}.json"))
                return null;

            var hybridCache = new HybridCache();

            string memKey = $"NextHUB:goInit:{plugin}";
            if (!hybridCache.TryGetValue(memKey, out NxtSettings init))
            {
                string json = $"{{{File.ReadAllText($"NextHUB/{plugin}.json")}}}";

                if (File.Exists($"NextHUB/{plugin}.my.json"))
                {
                    var target = JObject.Parse(json);
                    var mysource = JObject.Parse($"{{{File.ReadAllText($"NextHUB/{plugin}.my.json")}}}");

                    foreach (var property in mysource.Properties())
                    {
                        if (!target.ContainsKey(property.Name))
                        {
                            target[property.Name] = property.Value;
                            continue;
                        }

                        if (property.Value.Type == JTokenType.Object && target[property.Name].Type == JTokenType.Object)
                        {
                            var in1Json = (JObject)target[property.Name];
                            foreach (var p in ((JObject)property.Value).Properties())
                                in1Json[p.Name] = p.Value;
                        }
                        else
                        {
                            target[property.Name] = property.Value;
                        }
                    }

                    init = target.ToObject<NxtSettings>();
                }
                else
                {
                    init = JsonConvert.DeserializeObject<NxtSettings>(json);
                }

                if (string.IsNullOrEmpty(init.plugin))
                    init.plugin = init.displayname;

                if (!init.debug)
                    hybridCache.Set(memKey, init, DateTime.Now.AddMinutes(1));
            }

            return init;
        }
        #endregion

        #region Eval
        static EvalContext evalContext;
        
        public static EvalContext Eval
        {
            get
            {
                if (evalContext != null)
                    return evalContext;

                evalContext = new EvalContext();
                evalContext.RegisterAutoAddMissingTypeAssembly(typeof(Shared.Startup).Assembly);
                evalContext.RegisterAutoAddMissingTypeAssembly(typeof(Shared.Model.AppInit).Assembly);
                evalContext.AutoAddMissingTypes = true;

                return evalContext;
            }
        }
        #endregion
    }
}
