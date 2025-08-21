﻿using Lampac.Engine.CORE;
using Lampac.Models.Module;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.Extensions.DependencyModel;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine
{
    public static class CSharpEval
    {
        static InteractiveAssemblyLoader assemblyLoader = new InteractiveAssemblyLoader();
        static ConcurrentDictionary<string, dynamic> scripts = new ConcurrentDictionary<string, dynamic>();

        #region Execute
        public static T Execute<T>(in string cs, object model, ScriptOptions options = null)
        {
            return ExecuteAsync<T>(cs, model, options).GetAwaiter().GetResult();
        }

        public static Task<T> ExecuteAsync<T>(string cs, object model, ScriptOptions options = null)
        {
            var script = scripts.GetOrAdd(CrypTo.md5(cs), _ =>
            {
                if (options == null)
                    options = ScriptOptions.Default;

                options = options.AddReferences(typeof(Console).Assembly).AddImports("System")
                                 .AddReferences(typeof(HttpUtility).Assembly).AddImports("System.Web")
                                 .AddReferences(typeof(Enumerable).Assembly).AddImports("System.Linq")
                                 .AddReferences(typeof(List<>).Assembly).AddImports("System.Collections.Generic")
                                 .AddReferences(typeof(Regex).Assembly).AddImports("System.Text.RegularExpressions");

                return CSharpScript.Create<T>(
                    cs,
                    options,
                    globalsType: model.GetType(),
                    assemblyLoader: assemblyLoader
                ).CreateDelegate();
            });

            return script(model);
        }
        #endregion

        #region BaseExecute
        public static T BaseExecute<T>(in string cs, object model, ScriptOptions options = null, InteractiveAssemblyLoader loader = null)
        {
            return BaseExecuteAsync<T>(cs, model, options, loader).GetAwaiter().GetResult();
        }

        public static Task<T> BaseExecuteAsync<T>(string cs, object model, ScriptOptions options = null, InteractiveAssemblyLoader loader = null)
        {
            var script = scripts.GetOrAdd(CrypTo.md5(cs), _ =>
            {
                return CSharpScript.Create<T>(
                    cs,
                    options,
                    globalsType: model.GetType(),
                    assemblyLoader: loader
                ).CreateDelegate();
            });

            return script(model);
        }
        #endregion


        #region Compilation
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
        #endregion
    }
}
