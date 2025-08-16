using Lampac.Engine.CORE;
using Lampac.Models.Module;
using Lampac.Models.SISI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Playwright;
using Shared.Model.SISI;
using System.Collections.Concurrent;
using System.Reflection;

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
                        .AddImports("System.Web")
                        .AddImports("System.Linq")
                        .AddImports("System.Collections.Generic")
                        .AddImports("System.Text.RegularExpressions")

                        .AddReferences(typeof(Playwright).Assembly)
                        .AddImports(typeof(RouteContinueOptions).Namespace)

                        .AddReferences(typeof(Newtonsoft.Json.JsonConvert).Assembly)
                        .AddImports("Newtonsoft.Json")
                        .AddImports("Newtonsoft.Json.Linq")

                        .AddReferences(typeof(Bookmark).Assembly)
                        .AddImports(typeof(Bookmark).Namespace)

                        .AddReferences(typeof(MenuItem).Assembly)
                        .AddImports(typeof(MenuItem).Namespace);


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



        public static List<PortableExecutableReference> appReferences;

        public static Assembly Compilation(RootModule mod)
        {
            string path = $"{Environment.CurrentDirectory}/module/{mod.dll}";
            if (Directory.Exists(path))
            {
                lock (typeof(CSharpEval))
                {
                    var syntaxTree = new List<SyntaxTree>();

                    foreach (string file in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
                    {
                        if (file.Contains("/obj/"))
                            continue;

                        syntaxTree.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file)));
                    }

                    if (appReferences == null)
                    {
                        var dependencyContext = DependencyContext.Default;
                        var assemblies = dependencyContext.RuntimeLibraries
                            .SelectMany(library => library.GetDefaultAssemblyNames(dependencyContext))
                            .Select(Assembly.Load)
                            .ToList();

                        appReferences = assemblies.Select(assembly => MetadataReference.CreateFromFile(assembly.Location)).ToList();
                    }

                    if (mod.references != null)
                    {
                        foreach (string refns in mod.references)
                        {
                            string dlrns = Path.Combine(Environment.CurrentDirectory, "module", mod.dll, refns);
                            if (File.Exists(dlrns) && appReferences.FirstOrDefault(a => Path.GetFileName(a.FilePath) == refns) == null)
                            {
                                var assembly = Assembly.LoadFrom(dlrns);
                                appReferences.Add(MetadataReference.CreateFromFile(assembly.Location));
                            }
                        }
                    }

                    CSharpCompilation compilation = CSharpCompilation.Create(Path.GetFileName(mod.dll), syntaxTree, references: appReferences, options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                    using (var ms = new MemoryStream())
                    {
                        var result = compilation.Emit(ms);

                        if (result.Success)
                        {
                            ms.Seek(0, SeekOrigin.Begin);
                            return Assembly.Load(ms.ToArray());
                        }
                        else
                        {
                            Console.WriteLine($"\ncompilation error: {mod.dll}");
                            foreach (var diagnostic in result.Diagnostics)
                            {
                                if (diagnostic.Severity == DiagnosticSeverity.Error)
                                    Console.WriteLine(diagnostic);
                            }
                            Console.WriteLine("\n");
                        }
                    }
                }
            }

            return null;
        }
    }
}
