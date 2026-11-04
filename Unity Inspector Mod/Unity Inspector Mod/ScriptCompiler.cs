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
                { "mscorlib", "System.Core", "System", "System.Xml", "Microsoft.CSharp" };

        internal static readonly string[] PreambleNamespaces =
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Reflection",
            "UnityEngine",
            "HarmonyLib",
            "Unity_Inspector_Mod"
        };

        internal static readonly string[] PreambleAliases =
        {
            "Object = UnityEngine.Object"
        };

        private static bool propertyDefinePatched;
        private static readonly object propertyDefinePatchLock = new object();
        private static bool foreignBuilderTokensPatched;
        private static readonly object foreignBuilderTokensPatchLock = new object();

        private static readonly Regex EvalAssemblyName = new Regex(@"^eval-(\d+)");
        private static bool tokenCheckPatched;
        private static bool extensionMethodCrashPatched;
        private static bool assemblyImportPatched;
        private static readonly object assemblyImportPatchLock = new object();
        private static bool constantImportPatched;
        private static readonly object constantImportPatchLock = new object();
        private static bool assemblyLocationPatched;
        private static readonly object assemblyLocationPatchLock = new object();
        private static bool nestedTypeImportPatched;
        private static readonly object nestedTypeImportPatchLock = new object();

        private static readonly HashSet<string> reportedImportFailures = new HashSet<string>();

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
                // mcs's own AssemblyBuilderExtension.GetReferencedAssemblies() returns null on the Reflection.Emit path, and CheckReferencesPublicToken iterates it unguarded.
                harmony.Patch(checkMethod,
                    prefix: new HarmonyMethod(typeof(ScriptCompiler).GetMethod("SkipTokenCheck",
                        BindingFlags.Static | BindingFlags.NonPublic)));
                tokenCheckPatched = true;
            }
        }

        private static bool SkipTokenCheck()
        {
            return false;
        }

        // mcs's CS1061 extension-method search reads custom attributes, and mono_custom_attrs_get_attr -> mono_type_get_object kills Unity 2017.4's Mono natively.
        public static void PatchExtensionMethodCrash()
        {
            if (extensionMethodCrashPatched) return;
            var mcsAssembly = typeof(CSharpParser).Assembly;

            var rootNsType = mcsAssembly.GetType("Mono.CSharp.RootNamespace");
            if (rootNsType == null) return;

            var findMethod = rootNsType.GetMethod("FindExtensionMethodNamespaces",
                BindingFlags.Instance | BindingFlags.Public);

            if (findMethod != null)
            {
                var harmony = new Harmony("scriptcompiler.internal");
                harmony.Patch(findMethod,
                    prefix: new HarmonyMethod(typeof(ScriptCompiler).GetMethod("SkipExtensionMethodSearch",
                        BindingFlags.Static | BindingFlags.NonPublic)));
                extensionMethodCrashPatched = true;
            }
        }

        private static bool SkipExtensionMethodSearch()
        {
            return false;
        }

        // Any exception while importing one type drops the remaining types of that assembly; only a ReflectionTypeLoadException from GetTypes() is handled.
        public static void PatchAssemblyImport()
        {
            lock (assemblyImportPatchLock)
            {
                if (assemblyImportPatched) return;

                assemblyImportPatched = true;

                try
                {
                    var importMethod = typeof(ReflectionImporter).GetMethod("ImportAssembly",
                        BindingFlags.Instance | BindingFlags.Public,
                        null, new[] { typeof(Assembly), typeof(RootNamespace) }, null);

                    if (importMethod == null)
                    {
                        Main.Log("Mono.CSharp.ReflectionImporter.ImportAssembly not found — " + ImportDegradedWarning);
                        return;
                    }

                    var harmony = new Harmony("scriptcompiler.internal");
                    harmony.Patch(importMethod,
                        prefix: new HarmonyMethod(typeof(ScriptCompiler).GetMethod("ImportAssemblyPerType",
                            BindingFlags.Static | BindingFlags.NonPublic)));
                }
                catch (Exception e)
                {
                    Main.Log("Failed to patch Mono.CSharp.ReflectionImporter.ImportAssembly (" +
                             e.GetType().Name + ": " + e.Message + ") — " + ImportDegradedWarning);
                }
            }
        }

        private const string ImportDegradedWarning =
            "assembly import degraded to whole-assembly mode: any exception while importing one type " +
            "now silently drops the remaining types of its assembly (only a ReflectionTypeLoadException " +
            "from GetTypes() is handled), so scripts may fail with CS0246/CS0234 for types that exist";

        private static bool ImportAssemblyPerType(ReflectionImporter __instance, Assembly assembly,
            RootNamespace targetNamespace)
        {
            var assemblyName = SafeAssemblyName(assembly);

            try
            {
                __instance.GetAssemblyDefinition(assembly);
            }
            catch (Exception e)
            {
                ReportImportFailure(assemblyName, "assembly metadata", e);
                return false;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types; // Partially populated: unloadable entries are null
            }
            catch (Exception e)
            {
                ReportImportFailure(assemblyName, "type list", e);
                return false;
            }

            if (types == null)
            {
                ReportImportFailure(assemblyName, "type list", new TypeLoadException("no types could be loaded"));
                return false;
            }

            // UnityModManager Harmony-patches Assembly.GetTypes() to return an empty array for its own assembly.
            if (types.Length == 0)
            {
                types = GetTypesPerModule(assembly);
            }

            var single = new Type[1];
            foreach (var type in types)
            {
                if (type == null) continue;
                single[0] = type;
                try
                {
                    __instance.ImportTypes(single, targetNamespace, true);
                }
                catch (Exception e)
                {
                    ReportImportFailure(assemblyName, SafeTypeName(type), e);
                }
            }

            return false;
        }

        private static Type[] GetTypesPerModule(Assembly assembly)
        {
            Module[] modules;
            try
            {
                modules = assembly.GetModules(true);
            }
            catch (Exception)
            {
                return new Type[0];
            }

            var collected = new List<Type>();
            foreach (var module in modules)
            {
                Type[] moduleTypes;
                try
                {
                    moduleTypes = module.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    moduleTypes = e.Types; // Partially populated: unloadable entries are null
                }
                catch (Exception)
                {
                    continue;
                }

                if (moduleTypes == null) continue;

                foreach (var type in moduleTypes)
                {
                    if (type != null) collected.Add(type);
                }
            }

            return collected.ToArray();
        }

        public static void PatchConstantImport()
        {
            lock (constantImportPatchLock)
            {
                if (constantImportPatched) return;

                constantImportPatched = true;

                try
                {
                    var createMethod = typeof(MetadataImporter).GetMethod("CreateConstantFromValue",
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof(TypeSpec), typeof(FieldInfo) }, null);

                    if (createMethod == null)
                    {
                        Main.Log("Mono.CSharp.MetadataImporter.CreateConstantFromValue not found — " +
                                 ConstantImportWarning);
                        return;
                    }

                    var rawCalls = PatchProcessor.GetOriginalInstructions(createMethod)
                        .Count(instruction => instruction.Calls(GetRawConstantValueMethod));
                    if (rawCalls != 1)
                    {
                        Main.Log("Mono.CSharp.MetadataImporter.CreateConstantFromValue calls " +
                                 "FieldInfo.GetRawConstantValue " + rawCalls + " times, expected once — " +
                                 ConstantImportWarning);
                        return;
                    }

                    var harmony = new Harmony("scriptcompiler.internal");
                    harmony.Patch(createMethod,
                        transpiler: new HarmonyMethod(typeof(ScriptCompiler).GetMethod("ReadConstantValueTranspiler",
                            BindingFlags.Static | BindingFlags.NonPublic)));
                }
                catch (Exception e)
                {
                    Main.Log("Failed to patch Mono.CSharp.MetadataImporter.CreateConstantFromValue (" +
                             e.GetType().Name + ": " + e.Message + ") — " + ConstantImportWarning);
                }
            }
        }

        private const string ConstantImportWarning =
            "naming a const field or enum declared by a script or by execute_code will crash the game";

        private static readonly MethodInfo GetRawConstantValueMethod =
            typeof(FieldInfo).GetMethod("GetRawConstantValue");

        private static IEnumerable<CodeInstruction> ReadConstantValueTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var readConstantValue = typeof(ScriptCompiler).GetMethod("ReadConstantValue",
                BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(GetRawConstantValueMethod))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = readConstantValue;
                }

                yield return instruction;
            }
        }

        private static object ReadConstantValue(FieldInfo fi)
        {
            // Mono aborts on GetRawConstantValue for a field of an AssemblyBuilder assembly; GetValue(null) reads it.
            if (!(fi.DeclaringType.Assembly is AssemblyBuilder))
                return fi.GetRawConstantValue();
            var value = MemberPath.ReadField(fi, null);
            if (value != null && value.GetType().IsEnum)
                value = System.Convert.ChangeType(value, System.Enum.GetUnderlyingType(value.GetType()));
            return value;
        }

        public static void PatchAssemblyLocation()
        {
            lock (assemblyLocationPatchLock)
            {
                if (assemblyLocationPatched) return;

                assemblyLocationPatched = true;

                try
                {
                    // mcs reads an imported assembly's Location to word errors, and Assembly.Location throws for an AssemblyBuilder.
                    var locationGetter = typeof(ImportedAssemblyDefinition).GetProperty("Location",
                        BindingFlags.Instance | BindingFlags.Public)?.GetGetMethod();

                    if (locationGetter == null)
                    {
                        Main.Log("Mono.CSharp.ImportedAssemblyDefinition.Location not found — " + AssemblyLocationWarning);
                        return;
                    }

                    var harmony = new Harmony("scriptcompiler.internal");
                    harmony.Patch(locationGetter,
                        prefix: new HarmonyMethod(typeof(ScriptCompiler).GetMethod("DynamicAssemblyLocation",
                            BindingFlags.Static | BindingFlags.NonPublic)));
                }
                catch (Exception e)
                {
                    Main.Log("Failed to patch Mono.CSharp.ImportedAssemblyDefinition.Location (" +
                             e.GetType().Name + ": " + e.Message + ") — " + AssemblyLocationWarning);
                }
            }
        }

        private const string AssemblyLocationWarning =
            "a compiler error naming a member of a type declared by a script or by execute_code " +
            "will read as CS0584 (internal compiler error)";

        private static bool DynamicAssemblyLocation(ImportedAssemblyDefinition __instance, ref string __result)
        {
            if (!(__instance.Assembly is AssemblyBuilder)) return true;

            __result = SafeAssemblyName(__instance.Assembly);
            return false;
        }

        public static void PatchNestedTypeImport()
        {
            lock (nestedTypeImportPatchLock)
            {
                if (nestedTypeImportPatched) return;

                nestedTypeImportPatched = true;

                try
                {
                    var readerType = typeof(MetadataImporter).GetNestedType("AttributesTypeInfoReader",
                        BindingFlags.Public | BindingFlags.NonPublic);
                    var createType = readerType == null
                        ? null
                        : typeof(MetadataImporter).GetMethod("CreateType",
                            BindingFlags.Instance | BindingFlags.NonPublic, null,
                            new[] { typeof(Type), readerType, typeof(bool) }, null);

                    if (createType == null)
                    {
                        Main.Log("Mono.CSharp.MetadataImporter.CreateType(Type, AttributesTypeInfoReader, bool) not found — " +
                                 NestedTypeImportWarning);
                        return;
                    }

                    var reads = PatchProcessor.GetOriginalInstructions(createType).Count(ReadsDeclaringType);
                    if (reads != 2)
                    {
                        Main.Log("Mono.CSharp.MetadataImporter.CreateType reads Type.DeclaringType " + reads +
                                 " times, expected twice — " + NestedTypeImportWarning);
                        return;
                    }

                    var harmony = new Harmony("scriptcompiler.internal");
                    harmony.Patch(createType,
                        transpiler: new HarmonyMethod(typeof(ScriptCompiler).GetMethod("OpenDeclaringTypeTranspiler",
                            BindingFlags.Static | BindingFlags.NonPublic)));
                }
                catch (Exception e)
                {
                    Main.Log("Failed to patch Mono.CSharp.MetadataImporter.CreateType (" + e.GetType().Name + ": " +
                             e.Message + ") — " + NestedTypeImportWarning);
                }
            }
        }

        private const string NestedTypeImportWarning =
            "foreach over Dictionary.Keys, Dictionary.Values or a SortedDictionary will fail with CS0584 " +
            "(internal compiler error); copy the collection first, e.g. foreach (var v in new List<V>(d.Values))";

        private static bool ReadsDeclaringType(CodeInstruction instruction)
        {
            var method = instruction.operand as MethodInfo;
            return method != null && method.Name == "get_DeclaringType" &&
                   method.DeclaringType != null && method.DeclaringType.IsAssignableFrom(typeof(Type));
        }

        private static IEnumerable<CodeInstruction> OpenDeclaringTypeTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var openDeclaringType = typeof(ScriptCompiler).GetMethod("OpenDeclaringType",
                BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var instruction in instructions)
            {
                if (ReadsDeclaringType(instruction))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = openDeclaringType;
                }

                yield return instruction;
            }
        }

        private static Type OpenDeclaringType(Type type)
        {
            // Unity's Mono answers a nested generic instance's DeclaringType already inflated, and mcs inflates it again and throws.
            var declaring = type.DeclaringType;
            if (declaring == null || !declaring.IsGenericType || declaring.IsGenericTypeDefinition) return declaring;
            return declaring.GetGenericTypeDefinition();
        }

        public static void PatchPropertyDefine()
        {
            lock (propertyDefinePatchLock)
            {
                if (propertyDefinePatched) return;

                propertyDefinePatched = true;

                try
                {
                    var defineBuilders = typeof(PropertyBase).GetMethod("DefineBuilders",
                        BindingFlags.Instance | BindingFlags.NonPublic, null,
                        new[] { typeof(MemberKind), typeof(ParametersCompiled) }, null);
                    if (defineBuilders == null || PropertyBuilderField == null || CheckMissingAccessorMethod == null)
                    {
                        Main.Log("Mono.CSharp.PropertyBase members not found — " + PropertyDefineWarning);
                        return;
                    }

                    var harmony = new Harmony("scriptcompiler.internal");
                    // DefineBuilders calls a TypeBuilder.DefineProperty overload this mscorlib lacks, and Harmony cannot read a body that cannot JIT.
                    foreach (var caller in new[] { typeof(Property), typeof(Indexer) })
                    {
                        var define = caller.GetMethod("Define", BindingFlags.Instance | BindingFlags.Public,
                            null, Type.EmptyTypes, null);
                        int calls = define == null
                            ? 0
                            : PatchProcessor.GetOriginalInstructions(define).Count(i => i.Calls(defineBuilders));
                        if (calls != 1)
                        {
                            Main.Log("Mono.CSharp." + caller.Name + ".Define calls DefineBuilders " + calls +
                                     " times, expected once — " + PropertyDefineWarning);
                            continue;
                        }

                        harmony.Patch(define,
                            transpiler: new HarmonyMethod(typeof(ScriptCompiler).GetMethod("RedirectDefineBuilders",
                                BindingFlags.Static | BindingFlags.NonPublic)));
                    }
                }
                catch (Exception e)
                {
                    Main.Log("Failed to patch Mono.CSharp property definition (" + e.GetType().Name + ": " +
                             e.Message + ") — " + PropertyDefineWarning);
                }
            }
        }

        private const string PropertyDefineWarning =
            "properties, anonymous types and iterators will fail with MissingMethodException: TypeBuilder.DefineProperty";

        private static readonly FieldInfo PropertyBuilderField =
            typeof(PropertyBase).GetField("PropertyBuilder", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo CheckMissingAccessorMethod =
            typeof(PropertyBase).GetMethod("CheckMissingAccessor", BindingFlags.Instance | BindingFlags.NonPublic);

        private static IEnumerable<CodeInstruction> RedirectDefineBuilders(IEnumerable<CodeInstruction> instructions)
        {
            var defineBuilders = typeof(PropertyBase).GetMethod("DefineBuilders",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(MemberKind), typeof(ParametersCompiled) }, null);
            var replacement = typeof(ScriptCompiler).GetMethod("DefinePropertyBuilders",
                BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(defineBuilders))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }

                yield return instruction;
            }
        }

        private static void DefinePropertyBuilders(PropertyBase property, MemberKind kind, ParametersCompiled parameters)
        {
            var parent = property.Parent;
            // Mono encodes no calling convention in a property signature, so this overload loses nothing.
            var builder = parent.TypeBuilder.DefineProperty(
                property.GetFullName(property.MemberName), PropertyAttributes.None,
                property.MemberType.GetMetaInfo(), null, null,
                parameters.GetMetaInfo(), null, null);
            PropertyBuilderField.SetValue(property, builder);

            PropertySpec spec = kind == MemberKind.Indexer
                ? new IndexerSpec(parent.Definition, property, property.MemberType, parameters, builder, property.ModFlags)
                : new PropertySpec(kind, parent.Definition, property, property.MemberType, builder, property.ModFlags);

            if (property.Get != null)
            {
                spec.Get = property.Get.Spec;
                parent.MemberCache.AddMember(property, property.Get.Spec.Name, property.Get.Spec);
            }
            else
            {
                CheckMissingAccessorMethod.Invoke(property, new object[] { kind, parameters, true });
            }

            if (property.Set != null)
            {
                spec.Set = property.Set.Spec;
                parent.MemberCache.AddMember(property, property.Set.Spec.Name, property.Set.Spec);
            }
            else
            {
                CheckMissingAccessorMethod.Invoke(property, new object[] { kind, parameters, false });
            }

            parent.MemberCache.AddMember(property, builder.Name, spec);
        }

        public static void PatchForeignBuilderTokens()
        {
            lock (foreignBuilderTokensPatchLock)
            {
                if (foreignBuilderTokensPatched) return;

                foreignBuilderTokensPatched = true;

                try
                {
                    var fieldEmit = typeof(ILGenerator).GetMethod("Emit", new[] { typeof(OpCode), typeof(FieldInfo) });
                    var typeEmit = typeof(ILGenerator).GetMethod("Emit", new[] { typeof(OpCode), typeof(Type) });
                    if (fieldEmit == null || typeEmit == null || ILGeneratorModule == null)
                    {
                        Main.Log("System.Reflection.Emit.ILGenerator members not found — " + ForeignBuilderWarning);
                        return;
                    }

                    // Mono's mono_image_create_token gives another module's FieldBuilder or TypeBuilder a token of the emitting module.
                    var harmony = new Harmony("scriptcompiler.internal");
                    harmony.Patch(fieldEmit,
                        prefix: new HarmonyMethod(typeof(ScriptCompiler).GetMethod("ForeignFieldToken",
                            BindingFlags.Static | BindingFlags.NonPublic)));
                    harmony.Patch(typeEmit,
                        prefix: new HarmonyMethod(typeof(ScriptCompiler).GetMethod("ForeignTypeToken",
                            BindingFlags.Static | BindingFlags.NonPublic)));
                }
                catch (Exception e)
                {
                    Main.Log("Failed to patch System.Reflection.Emit.ILGenerator.Emit (" + e.GetType().Name + ": " +
                             e.Message + ") — " + ForeignBuilderWarning);
                }
            }
        }

        private const string ForeignBuilderWarning =
            "execute_code naming a static of a type an earlier execute_code declared may read or write the wrong field";

        private static readonly FieldInfo ILGeneratorModule =
            typeof(ILGenerator).GetField("module", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Type CreatedForeignType(ILGenerator generator, Type type)
        {
            var builder = type as TypeBuilder;
            if (builder == null || !builder.IsCreated() || builder.IsGenericTypeDefinition) return null;
            if (builder.Module == (Module)ILGeneratorModule.GetValue(generator)) return null;
            return builder.CreateType();
        }

        private static void ForeignFieldToken(ILGenerator __instance, ref FieldInfo field)
        {
            if (!(field is FieldBuilder)) return;

            var created = CreatedForeignType(__instance, field.DeclaringType);
            if (created == null) return;

            var runtimeField = created.GetField(field.Name,
                BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic);
            if (runtimeField != null) field = runtimeField;
        }

        private static void ForeignTypeToken(ILGenerator __instance, ref Type cls)
        {
            var created = CreatedForeignType(__instance, cls);
            if (created != null) cls = created;
        }

        internal static string SafeAssemblyName(Assembly assembly)
        {
            try
            {
                return assembly.GetName().Name;
            }
            catch (Exception)
            {
                return "<unknown assembly>";
            }
        }

        private static string SafeTypeName(Type type)
        {
            try
            {
                return "type '" + type.FullName + "'";
            }
            catch (Exception)
            {
                return "type '<unknown>'";
            }
        }

        internal static void ReportImportFailure(string assemblyName, string what, Exception e)
        {
            var key = assemblyName + "|" + e.GetType().Name;
            if (!reportedImportFailures.Add(key)) return;

            Main.Log("Script compiler could not import " + what + " from assembly '" + assemblyName +
                     "': " + e.GetType().Name + ": " + e.Message +
                     " (further " + e.GetType().Name + "s from this assembly will not be logged)");
        }

        private static string DetectEnumUsageError(string name, string source)
        {
            bool ownEnum;
            var visible = EvalAssemblies().Concat(ScriptManager.LoadedScriptAssemblies(name));
            var referenced = FindEnumUsage(source, DynamicEnumNames(visible), out ownEnum);
            if (referenced == null) return null;
            if (!ownEnum) return DynamicEnumUsageMessage(referenced);

            return "Script defines enum '" + referenced + "' and references it within the same script.\n" +
                   "Two shapes are measured to abort Unity 2017.4's Mono: as the type of a static field the " +
                   "enum kills the game during compilation (native crash, mono_reflection_get_token in the " +
                   "native stack), and as the type of a method local it compiles and kills the game when the " +
                   "script runs. Every other in-script reference is blocked here as unmeasured.\n" +
                   "Workarounds:\n" +
                   "  - Define the enum in a mod DLL or non-script source\n" +
                   "  - Use 'int' instead of '" + referenced + "' for fields/locals/parameters, casting to '" + referenced + "' when needed\n" +
                   "  - Use string identifiers instead of an enum";
        }

        internal static string DynamicEnumUsageMessage(string enumName)
        {
            return "Enum '" + enumName + "' is defined by a script or by execute_code, and cannot be named from " +
                   "execute_code or from another script.\n" +
                   "A local of its type kills the game while Mono compiles the method, and a script's enum named " +
                   "from execute_code has its members typed int.\n" +
                   "Workarounds:\n" +
                   "  - Use the enum's underlying values instead of naming '" + enumName + "'\n" +
                   "  - Define the enum in a mod DLL or non-script source";
        }

        internal static string FindEnumUsage(string source, IEnumerable<string> dynamicEnums, out bool ownEnum)
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

            ownEnum = true;
            var referenced = FindEnumReference(masked, enumDecls.Select(decl => decl.Key));
            if (referenced != null) return referenced;

            ownEnum = false;
            return FindEnumReference(masked, dynamicEnums);
        }

        private static string FindEnumReference(string cleanSource, IEnumerable<string> enumNames)
        {
            foreach (var enumName in enumNames)
            {
                if (Regex.IsMatch(cleanSource, @"\b" + Regex.Escape(enumName) + @"\b"))
                {
                    return enumName;
                }
            }

            return null;
        }

        internal static List<string> DynamicEnumNames()
        {
            return DynamicEnumNames(AppDomain.CurrentDomain.GetAssemblies().Where(a => a is AssemblyBuilder));
        }

        private static List<string> DynamicEnumNames(IEnumerable<Assembly> assemblies)
        {
            var names = new List<string>();
            foreach (var assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types; // Partially populated: unloadable entries are null
                }
                catch (Exception)
                {
                    continue; // An AssemblyBuilder still being emitted can throw
                }

                if (types == null) continue;

                foreach (var type in types)
                {
                    if (type != null && type.IsEnum) names.Add(type.Name);
                }
            }

            return names;
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

        internal static string PreambleSource(string source)
        {
            var preamble = new StringBuilder();
            foreach (var ns in PreambleNamespaces)
            {
                preamble.Append("using ").Append(ns).Append(";\n");
            }
            var clean = StripCommentsAndStrings(source);
            foreach (var alias in PreambleAliases)
            {
                var aliasName = alias.Substring(0, alias.IndexOf('=')).Trim();
                if (Regex.IsMatch(clean, @"\busing\s+" + Regex.Escape(aliasName) + @"\s*=")) continue;

                preamble.Append("using ").Append(alias).Append(";\n");
            }
            preamble.Append("#line 1\n");
            return preamble.ToString();
        }

        public static CompileResult Compile(string name, string source)
        {
            var enumError = DetectEnumUsageError(name, source);
            if (enumError != null)
            {
                return new CompileResult
                {
                    Success = false,
                    Errors = enumError
                };
            }

            PatchPropertyDefine();

            var errorOutput = new StringBuilder();
            var reporter = new StreamReportPrinter(new StringWriter(errorOutput));

            Location.Reset();

            var dllName = "script_" + name + "_" + DateTime.Now.Ticks;

            var settings = new CompilerSettings
            {
                Version = LanguageVersion.Experimental,
                GenerateDebugInfo = false,
                StdLib = true,
                Target = Target.Library,
                WarningLevel = 0,
                EnhancedWarnings = false,
                // Otherwise an internal error is reported as CS0584 with its cause dropped.
                BreakOnInternalError = true
            };

            var ctx = new CompilerContext(settings, reporter);
            ctx.Settings.SourceFiles.Clear();

            source = PreambleSource(source) + source;

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
                foreach (var script in ScriptManager.LoadedScriptAssemblies(name))
                {
                    try
                    {
                        importer.ImportAssembly(script, container.GlobalRootNamespace);
                    }
                    catch (Exception e)
                    {
                        ReportImportFailure(SafeAssemblyName(script), "any type", e);
                    }
                }
                try
                {
                    importer.ImportTypes(LatestEvalTypes(), container.GlobalRootNamespace, true);
                }
                catch (Exception e)
                {
                    ReportImportFailure("eval", "execute_code types", e);
                }

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

                // Method bodies are resolved during Emit; closing the container after an error there fails on the bodyless method.
                if (ctx.Report.Errors > 0)
                {
                    return new CompileResult
                    {
                        Success = false,
                        Errors = errorOutput.ToString()
                    };
                }

                container.CloseContainer();
                ass.EmbedResources();

                var lambdaError = DetectEmptyGenericLambdas(ass.Builder);
                if (lambdaError != null)
                {
                    return new CompileResult
                    {
                        Success = false,
                        Errors = lambdaError
                    };
                }

                return new CompileResult
                {
                    Success = true,
                    Assembly = ass.Builder
                };
            }
            catch (Exception e)
            {
                Main.Log("Script compiler failure in '" + name + "':\n" + DescribeExceptionChain(e));

                return new CompileResult
                {
                    Success = false,
                    Errors = ctx.Report.Errors > 0 ? errorOutput.ToString() : DescribeInternalError(e)
                };
            }
            finally
            {
                RootContext.ToplevelTypes = savedToplevel;
            }
        }

        private const int ExceptionChainLimit = 32;

        private static List<Exception> ExceptionChain(Exception e)
        {
            var chain = new List<Exception>();
            for (var current = e; current != null && chain.Count < ExceptionChainLimit; current = current.InnerException)
            {
                chain.Add(current);
            }
            return chain;
        }

        private static readonly Regex InternalErrorLocation = new Regex(@"^\S*\(\d+(,\d+\+?)?\):");

        internal static string DescribeInternalError(Exception e)
        {
            var chain = ExceptionChain(e);
            var cause = chain[chain.Count - 1];

            var sb = new StringBuilder();
            sb.Append("Internal compiler error: ").Append(cause.GetType().FullName).Append(": ").Append(cause.Message);

            for (int i = 0; i < chain.Count - 1; i++)
            {
                if (!(chain[i] is InternalErrorException)) continue;

                var where = chain[i].Message.Trim();
                if (where.EndsWith(":")) continue;
                sb.Append(InternalErrorLocation.IsMatch(where) ? "\n  while compiling " : "\n  ").Append(where);
            }

            return sb.ToString();
        }

        private static string DescribeExceptionChain(Exception e)
        {
            if (e == null) return "no exception";

            var chain = ExceptionChain(e);
            var innermost = chain[chain.Count - 1];
            var sb = new StringBuilder();
            sb.Append("Cause: ").Append(innermost.GetType().FullName).Append(": ").Append(innermost.Message);
            if (!string.IsNullOrEmpty(innermost.StackTrace))
            {
                sb.Append("\n").Append(innermost.StackTrace);
            }

            if (innermost.InnerException != null)
            {
                sb.Append("\n... chain truncated at ").Append(ExceptionChainLimit).Append(" levels");
            }

            for (int i = chain.Count - 2; i >= 0; i--)
            {
                sb.Append("\nWrapped by: ").Append(chain[i].GetType().FullName)
                    .Append(": ").Append(chain[i].Message);
                if (!string.IsNullOrEmpty(chain[i].StackTrace))
                {
                    sb.Append("\n").Append(chain[i].StackTrace);
                }
            }

            return sb.ToString();
        }

        internal static string DetectEmptyGenericLambdas(Assembly assembly)
        {
            var found = new List<string>();
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }

            var pending = new Stack<Type>(types.Where(t => t != null));
            while (pending.Count > 0)
            {
                var type = pending.Pop();
                foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                {
                    pending.Push(nested);
                }

                foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static |
                                                       BindingFlags.Instance | BindingFlags.Public |
                                                       BindingFlags.NonPublic))
                {
                    // mcs hosts a non-capturing lambda of a generic method as a generic <Method>m__N, which Unity's Mono runs with an empty body.
                    if (!method.IsGenericMethodDefinition || !method.Name.StartsWith("<")) continue;

                    int close = method.Name.IndexOf(">m__", StringComparison.Ordinal);
                    if (close < 0) continue;

                    found.Add(type.FullName + "." + method.Name.Substring(1, close - 1));
                }
            }

            if (found.Count == 0) return null;

            return "Generic method " + string.Join(", ", found.Distinct().ToArray()) +
                   " holds a lambda that captures nothing, which this runtime compiles with an empty body " +
                   "(InvalidProgramException: method body is empty, when it runs).\n" +
                   "Workarounds:\n" +
                   "  - Make the lambda use a parameter or local of the method, so it captures\n" +
                   "  - Move the lambda into a non-generic method";
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
                .Where(a => !(a is AssemblyBuilder))
                .Select(a => new { ass = a, name = ParseName(a.FullName) })
                .Where(a => a.name != null)
                .GroupBy(a => a.name.Name)
                .Select(g => g.OrderByDescending(a => a.name.Version).First());

            foreach (var ass in dedupedAssemblies)
            {
                if (StdLib.Contains(ass.name.Name))
                    continue;
                try
                {
                    import(ass.ass);
                }
                catch (Exception e)
                {
                    ReportImportFailure(ass.name.Name, "any type", e);
                }
            }
        }

        private static IEnumerable<Assembly> EvalAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a is AssemblyBuilder && EvalAssemblyName.IsMatch(SafeAssemblyName(a)));
        }

        internal static Assembly NewestEvalAssembly()
        {
            Assembly newest = null;
            int newestNumber = -1;
            foreach (var assembly in EvalAssemblies())
            {
                int number;
                if (int.TryParse(EvalAssemblyName.Match(SafeAssemblyName(assembly)).Groups[1].Value, out number)
                    && number > newestNumber)
                {
                    newest = assembly;
                    newestNumber = number;
                }
            }
            return newest;
        }

        internal static Type[] LatestEvalTypes()
        {
            var latest = new Dictionary<string, KeyValuePair<int, Type>>();
            foreach (var assembly in EvalAssemblies())
            {
                int number;
                if (!int.TryParse(EvalAssemblyName.Match(SafeAssemblyName(assembly)).Groups[1].Value, out number))
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }
                catch (Exception)
                {
                    continue;
                }

                if (types == null) continue;

                foreach (var type in types)
                {
                    if (type == null || type is TypeBuilder || type.IsNested) continue;

                    string fullName = type.FullName;
                    if (fullName == null || fullName.IndexOf('<') >= 0) continue;

                    KeyValuePair<int, Type> held;
                    if (!latest.TryGetValue(fullName, out held) || held.Key < number)
                    {
                        latest[fullName] = new KeyValuePair<int, Type>(number, type);
                    }
                }
            }

            return latest.Values.Select(v => v.Value).ToArray();
        }
    }
}
