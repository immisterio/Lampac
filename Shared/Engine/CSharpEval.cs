using Lampac.Engine.CORE;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.Playwright;
using System.Collections.Concurrent;

namespace Shared.Engine
{
    public static class CSharpEval
    {
        static ConcurrentDictionary<string, dynamic> scripts = new ConcurrentDictionary<string, dynamic>();

        public static T Execute<T>(in string cs, object model)
        {
            return ExecuteAsync<T>(cs, model).GetAwaiter().GetResult();
        }

        public static Task<T> ExecuteAsync<T>(string cs, object model)
        {
            var script = scripts.GetOrAdd(CrypTo.md5(cs), _ =>
            {
                using (var loader = new InteractiveAssemblyLoader())
                {
                    var options = Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default
                        .AddImports("System")
                        .AddImports("System.Collections.Generic")
                        .AddImports("System.Linq")
                        .AddImports("System.Text.RegularExpressions")

                        .AddReferences(typeof(Playwright).Assembly)
                        .AddImports(typeof(RouteContinueOptions).Namespace);


                    return CSharpScript.Create<T>(
                        cs,
                        options,
                        globalsType: model.GetType(),
                        assemblyLoader: loader
                    ).CreateDelegate();
                }
            });

            return script(model);
        }
    }
}
