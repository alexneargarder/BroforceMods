using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using Mono.CSharp;

namespace Unity_Inspector_Mod
{
    public static class ScriptCompiler
    {
        private static readonly HashSet<string> StdLib =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "mscorlib", "System.Core", "System", "System.Xml" };

        private static readonly HashSet<string> CompiledAssemblies = new HashSet<string>();
        private static bool tokenCheckPatched;

        public class CompileResult
        {
            public bool Success;
            public Assembly Assembly;
            public string Errors;
        }

        public static void PatchTokenCheck()
        {
            if (tokenCheckPatched) return;
            var mcsAssembly = typeof(CSharpParser).Assembly;
            var assemblyDefType = mcsAssembly.GetType("Mono.CSharp.AssemblyDefinition");
            var checkMethod = assemblyDefType.GetMethod("CheckReferencesPublicToken",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (checkMethod != null)
            {
                var harmony = new Harmony("scriptcompiler.internal");
                harmony.Patch(checkMethod,
                    prefix: new HarmonyMethod(typeof(ScriptCompiler).GetMethod("SkipTokenCheck",
                        BindingFlags.Static | BindingFlags.NonPublic)));
                tokenCheckPatched = true;
            }
        }

        private static bool SkipTokenCheck()
        {
            return false; // Skip original method
        }

        // Unity 2017.4's Mono runtime crashes (native segfault in mono_reflection_get_token)
        // when an enum defined in a dynamically compiled assembly is referenced anywhere by
        // running code. Compilation may succeed but the JIT crashes when the enum's metadata
        // needs to be resolved. Known Mono bug fixed upstream in 5.10 (Xamarin Bugzilla #59080)
        // but Unity 2017.4 ships an older Mono. Detect script-defined enums that are also
        // referenced within the same script and fail with a clear error before reaching the
        // compiler.
        private static string DetectEnumUsageError(string source)
        {
            var clean = StripCommentsAndStrings(source);

            var enumDecls = new List<KeyValuePair<string, int[]>>();
            foreach (Match m in Regex.Matches(clean, @"\benum\s+(\w+)"))
            {
                var enumName = m.Groups[1].Value;
                var braceIdx = clean.IndexOf('{', m.Index + m.Length);
                if (braceIdx < 0) continue;
                int depth = 1;
                int endIdx = braceIdx + 1;
                while (endIdx < clean.Length && depth > 0)
                {
                    if (clean[endIdx] == '{') depth++;
                    else if (clean[endIdx] == '}') depth--;
                    endIdx++;
                }
                if (depth != 0) continue;
                enumDecls.Add(new KeyValuePair<string, int[]>(enumName, new[] { m.Index, endIdx }));
            }

            if (enumDecls.Count == 0) return null;

            // Mask out declaration regions so the usage check only sees other references
            var sb = new StringBuilder(clean);
            foreach (var decl in enumDecls)
            {
                int start = decl.Value[0];
                int end = decl.Value[1];
                for (int i = start; i < end && i < sb.Length; i++)
                {
                    if (sb[i] != '\n') sb[i] = ' ';
                }
            }
            var masked = sb.ToString();

            foreach (var decl in enumDecls)
            {
                var enumName = decl.Key;
                if (Regex.IsMatch(masked, @"\b" + Regex.Escape(enumName) + @"\b"))
                {
                    return "Script defines enum '" + enumName + "' and references it within the same script.\n" +
                           "This crashes Unity 2017.4's Mono runtime due to a known bug (Xamarin Bugzilla #59080) " +
                           "that cannot be worked around at the script level.\n" +
                           "Workarounds:\n" +
                           "  - Define the enum in a mod DLL or non-script source\n" +
                           "  - Use 'int' instead of '" + enumName + "' for fields/locals/parameters, casting to '" + enumName + "' when needed\n" +
                           "  - Use string identifiers instead of an enum";
                }
            }

            return null;
        }

        private static string StripCommentsAndStrings(string source)
        {
            var sb = new StringBuilder(source.Length);
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n') { sb.Append(' '); i++; }
                    continue;
                }

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    sb.Append("  ");
                    i += 2;
                    while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                    {
                        sb.Append(source[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    if (i + 1 < source.Length) { sb.Append("  "); i += 2; }
                    continue;
                }

                if (c == '@' && i + 1 < source.Length && source[i + 1] == '"')
                {
                    sb.Append("  ");
                    i += 2;
                    while (i < source.Length)
                    {
                        if (source[i] == '"' && (i + 1 >= source.Length || source[i + 1] != '"')) { sb.Append(' '); i++; break; }
                        if (source[i] == '"' && source[i + 1] == '"') { sb.Append("  "); i += 2; continue; }
                        sb.Append(source[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    continue;
                }

                if (c == '"')
                {
                    sb.Append(' ');
                    i++;
                    while (i < source.Length && source[i] != '"')
                    {
                        if (source[i] == '\\' && i + 1 < source.Length) { sb.Append("  "); i += 2; continue; }
                        sb.Append(' ');
                        i++;
                    }
                    if (i < source.Length) { sb.Append(' '); i++; }
                    continue;
                }

                if (c == '\'')
                {
                    sb.Append(' ');
                    i++;
                    while (i < source.Length && source[i] != '\'')
                    {
                        if (source[i] == '\\' && i + 1 < source.Length) { sb.Append("  "); i += 2; continue; }
                        sb.Append(' ');
                        i++;
                    }
                    if (i < source.Length) { sb.Append(' '); i++; }
                    continue;
                }

                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        public static CompileResult Compile(string name, string source)
        {
            var enumError = DetectEnumUsageError(source);
            if (enumError != null)
            {
                return new CompileResult
                {
                    Success = false,
                    Errors = enumError
                };
            }

            var errorOutput = new StringBuilder();
            var reporter = new StreamReportPrinter(new StringWriter(errorOutput));

            Location.Reset();

            var dllName = "script_" + name + "_" + DateTime.Now.Ticks;
            CompiledAssemblies.Add(dllName);

            var settings = new CompilerSettings
            {
                Version = LanguageVersion.Experimental,
                GenerateDebugInfo = false,
                StdLib = true,
                Target = Target.Library,
                WarningLevel = 0,
                EnhancedWarnings = false
            };

            var ctx = new CompilerContext(settings, reporter);
            ctx.Settings.SourceFiles.Clear();

            // Auto-import common namespaces so scripts don't need boilerplate
            var preamble =
                "using System;\n" +
                "using System.Collections.Generic;\n" +
                "using System.Linq;\n" +
                "using System.Reflection;\n" +
                "using UnityEngine;\n" +
                "using HarmonyLib;\n" +
                "using Unity_Inspector_Mod;\n";
            source = preamble + source;

            var sourceBytes = Encoding.UTF8.GetBytes(source);
            var fileName = name + ".cs";

            SeekableStreamReader GetFile(SourceFile file)
            {
                return new SeekableStreamReader(new MemoryStream(sourceBytes), Encoding.UTF8);
            }

            ctx.Settings.SourceFiles.Add(new SourceFile(fileName, fileName, 0, GetFile));

            var savedToplevel = RootContext.ToplevelTypes;
            try
            {
                var container = new ModuleContainer(ctx);
                RootContext.ToplevelTypes = container;
                Location.Initialize(ctx.Settings.SourceFiles);

                var session = new ParserSession { UseJayGlobalArrays = true, LocatedTokens = new LocatedToken[15000] };
                container.EnableRedefinition();

                foreach (var sourceFile in ctx.Settings.SourceFiles)
                {
                    var stream = sourceFile.GetInputStream(sourceFile);
                    var compilationSource = new CompilationSourceFile(container, sourceFile);
                    compilationSource.EnableRedefinition();
                    container.AddTypeContainer(compilationSource);
                    var parser = new CSharpParser(stream, compilationSource, session);
                    parser.parse();
                }

                var ass = new AssemblyDefinitionDynamic(container, dllName, dllName + ".dll");
                container.SetDeclaringAssembly(ass);

                var importer = new ReflectionImporter(container, ctx.BuiltinTypes)
                {
                    IgnoreCompilerGeneratedField = true,
                    IgnorePrivateMembers = false
                };
                ass.Importer = importer;

                // DynamicLoader is internal in this mcs.dll, so use reflection
                var mcsAssembly = typeof(CSharpParser).Assembly;
                var dynamicLoaderType = mcsAssembly.GetType("Mono.CSharp.DynamicLoader");
                var loader = Activator.CreateInstance(dynamicLoaderType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                    null, new object[] { importer, ctx }, null);

                ImportAppDomainAssemblies(a => importer.ImportAssembly(a, container.GlobalRootNamespace));

                dynamicLoaderType.GetMethod("LoadReferences",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(loader, new object[] { container });
                ass.Create(AppDomain.CurrentDomain, AssemblyBuilderAccess.RunAndSave);
                container.CreateContainer();
                dynamicLoaderType.GetMethod("LoadModules",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(loader, new object[] { ass, container.GlobalRootNamespace });
                container.InitializePredefinedTypes();
                container.Define();

                if (ctx.Report.Errors > 0)
                {
                    return new CompileResult
                    {
                        Success = false,
                        Errors = errorOutput.ToString()
                    };
                }

                ass.Resolve();
                ass.Emit();
                container.CloseContainer();
                ass.EmbedResources();

                return new CompileResult
                {
                    Success = true,
                    Assembly = ass.Builder
                };
            }
            catch (Exception e)
            {
                var innerMsg = e.InnerException != null ? "\nInner: " + e.InnerException.Message + "\n" + e.InnerException.StackTrace : "";
                return new CompileResult
                {
                    Success = false,
                    Errors = errorOutput.Length > 0
                        ? errorOutput.ToString() + "\n" + e.Message + "\n" + e.StackTrace + innerMsg
                        : e.Message + "\n" + e.StackTrace + innerMsg
                };
            }
            finally
            {
                RootContext.ToplevelTypes = savedToplevel;
            }
        }

        private static AssemblyName ParseName(string fullName)
        {
            try
            {
                return new AssemblyName(fullName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ImportAppDomainAssemblies(Action<Assembly> import)
        {
            var dedupedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => new { ass = a, name = ParseName(a.FullName) })
                .Where(a => a.name != null)
                .GroupBy(a => a.name.Name)
                .Select(g => g.OrderByDescending(a => a.name.Version).First());

            foreach (var ass in dedupedAssemblies)
            {
                if (StdLib.Contains(ass.name.Name) || CompiledAssemblies.Contains(ass.name.Name))
                    continue;
                try
                {
                    import(ass.ass);
                }
                catch (Exception)
                {
                    // Some assemblies may fail to import
                }
            }
        }
    }
}
