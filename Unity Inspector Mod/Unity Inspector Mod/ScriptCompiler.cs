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
            new HashSet<string>( StringComparer.OrdinalIgnoreCase )
                { "mscorlib", "System.Core", "System", "System.Xml", "Microsoft.CSharp" };

        internal static readonly string[] PreambleNamespaces =
        {
            "System",
            "System.Collections",
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

        private static readonly Regex EvalAssemblyName = new Regex( @"^eval-(\d+)" );
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

        internal static void InstallPatches()
        {
            PatchTokenCheck();
            PatchExtensionMethodCrash();
            PatchAssemblyImport();
            PatchConstantImport();
            PatchAssemblyLocation();
            PatchNestedTypeImport();
            PatchPropertyDefine();
            PatchForeignBuilderTokens();
            PatchGenericParameterImport();
            PatchLoopStopPoints();
            PatchMethodEntryChecks();
            PatchExceptionFilters();
            PatchMethodEndPad();
            PatchScriptMethodBodyGuard();
            PatchUnloadableMembers();
            PatchGuardedReflection();
            PatchMemberToString();
            PatchGenericInstanceFields();
            PatchJsonFieldProvider();
            PatchGenericMethodBinding();
        }

        internal static int InstrumentCode;

        private static bool loopStopPointsPatched;

        private static void PatchLoopStopPoints()
        {
            if ( loopStopPointsPatched )
            {
                return;
            }
            loopStopPointsPatched = true;

            try
            {
                var branchEmit = typeof( ILGenerator ).GetMethod( "Emit", new[] { typeof( OpCode ), typeof( Label ) } );
                if ( branchEmit == null || ILGeneratorLabels == null || LabelIndex == null || LabelAddress == null )
                {
                    Main.Log( "System.Reflection.Emit.ILGenerator label members not found — " + LoopStopWarning );
                    return;
                }

                var harmony = new Harmony( "scriptcompiler.internal" );
                harmony.Patch( branchEmit,
                    prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "EmitLoopStopPoint",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch System.Reflection.Emit.ILGenerator.Emit(OpCode, Label) (" +
                          e.GetType().Name + ": " + e.Message + ") — " + LoopStopWarning );
            }
        }

        private const string LoopStopWarning =
            "an execute_code call, Main() or Unload() caught in a loop cannot be stopped at its budget";

        private static readonly FieldInfo ILGeneratorLabels =
            typeof( ILGenerator ).GetField( "labels", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly FieldInfo ILGeneratorLabelCount =
            typeof( ILGenerator ).GetField( "num_labels", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly FieldInfo ILGeneratorCurStack =
            typeof( ILGenerator ).GetField( "cur_stack", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly FieldInfo ILGeneratorCodeLength =
            typeof( ILGenerator ).GetField( "code_len", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly FieldInfo ILGeneratorCode =
            typeof( ILGenerator ).GetField( "code", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly FieldInfo LabelAddress =
            typeof( ILGenerator ).GetNestedType( "LabelData", BindingFlags.NonPublic )?.GetField( "addr" );

        private static readonly FieldInfo LabelIndex =
            typeof( Label ).GetField( "label", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly MethodInfo StopPointMethod =
            typeof( MainThreadDispatcher ).GetMethod( "StopPoint", BindingFlags.Static | BindingFlags.Public );

        // A branch to a label already marked jumps backward, which every loop does once per iteration.
        private static void EmitLoopStopPoint( ILGenerator __instance, OpCode opcode, Label label )
        {
            if ( InstrumentCode <= 0 )
            {
                return;
            }
            if ( opcode.FlowControl != FlowControl.Branch && opcode.FlowControl != FlowControl.Cond_Branch )
            {
                return;
            }
            if ( opcode.OperandType != OperandType.InlineBrTarget )
            {
                return;
            }

            var labels = ILGeneratorLabels.GetValue( __instance ) as Array;
            int index = (int)LabelIndex.GetValue( label );
            if ( labels == null || index < 0 || index >= labels.Length )
            {
                return;
            }

            if ( (int)LabelAddress.GetValue( labels.GetValue( index ) ) < 0 )
            {
                return;
            }

            __instance.Emit( OpCodes.Call, StopPointMethod );
        }

        private static bool methodEntryChecksPatched;

        private static void PatchMethodEntryChecks()
        {
            if ( methodEntryChecksPatched )
            {
                return;
            }
            methodEntryChecksPatched = true;

            Harmony harmony = new Harmony( "scriptcompiler.internal" );
            try
            {
                harmony.Patch( typeof( ILGenerator ).GetConstructors( BindingFlags.Instance | BindingFlags.NonPublic ).Single(),
                    postfix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "EmitMethodEntryCheck",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch the System.Reflection.Emit.ILGenerator constructor (" + e.GetType().Name + ": " +
                          e.Message + ") — code compiled by execute_code or execute_script that recurses without end kills the game" );
            }

            try
            {
                harmony.Patch( typeof( ILGenerator ).GetMethod( "BeginFinallyBlock", Type.EmptyTypes ),
                    postfix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "EmitFinallySkip",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch System.Reflection.Emit.ILGenerator.BeginFinallyBlock (" + e.GetType().Name + ": " +
                          e.Message + ") — code compiled by execute_code or execute_script that recurses or throws without end inside a finally block kills the game" );
            }

            try
            {
                if ( ILGeneratorCurStack == null )
                {
                    throw new MissingFieldException( "System.Reflection.Emit.ILGenerator", "cur_stack" );
                }
                // The prefix goes first: a filter skip whose end label nothing marks fails every compile with a catch filter.
                harmony.Patch( typeof( ILGenerator ).GetMethod( "BeginCatchBlock", new[] { typeof( Type ) } ),
                    prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "MarkFilterEnd",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
                harmony.Patch( typeof( ILGenerator ).GetMethod( "BeginExceptFilterBlock", Type.EmptyTypes ),
                    postfix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "EmitFilterSkip",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch System.Reflection.Emit.ILGenerator.BeginExceptFilterBlock or BeginCatchBlock (" +
                          e.GetType().Name + ": " + e.Message +
                          ") — code compiled by execute_code or execute_script that recurses or throws without end inside a catch's when filter kills the game" );
            }
        }

        private static readonly MethodInfo MethodEntryMethod =
            typeof( MainThreadDispatcher ).GetMethod( "MethodEntry", BindingFlags.Static | BindingFlags.Public );

        private static readonly MethodInfo SkipHandlerMethod =
            typeof( MainThreadDispatcher ).GetMethod( "SkipHandler", BindingFlags.Static | BindingFlags.Public );

        private static readonly Dictionary<ILGenerator, Label> pendingFilterEnds = new Dictionary<ILGenerator, Label>();

        private static void EmitMethodEntryCheck( ILGenerator __instance )
        {
            if ( InstrumentCode > 0 )
            {
                __instance.Emit( OpCodes.Call, MethodEntryMethod );
            }
        }

        private static void EmitFinallySkip( ILGenerator __instance )
        {
            if ( InstrumentCode <= 0 )
            {
                return;
            }

            Label run = __instance.DefineLabel();
            __instance.Emit( OpCodes.Call, SkipHandlerMethod );
            __instance.Emit( OpCodes.Brfalse_S, run );
            __instance.Emit( OpCodes.Endfinally );
            __instance.MarkLabel( run );
        }

        // A filter starts with the exception on the stack and ends at the endfilter BeginCatchBlock(null) writes; a skipped one answers 0, not this catch.
        private static void EmitFilterSkip( ILGenerator __instance )
        {
            if ( InstrumentCode <= 0 )
            {
                return;
            }

            // Mono's ILGenerator does not count the exception a filter starts with, and the check's result sits on top of it.
            ILGeneratorCurStack.SetValue( __instance, (int)ILGeneratorCurStack.GetValue( __instance ) + 1 );

            Label run = __instance.DefineLabel();
            Label end = __instance.DefineLabel();
            lock ( pendingFilterEnds )
            {
                pendingFilterEnds[__instance] = end;
            }

            __instance.Emit( OpCodes.Call, SkipHandlerMethod );
            __instance.Emit( OpCodes.Brfalse_S, run );
            __instance.Emit( OpCodes.Pop );
            __instance.Emit( OpCodes.Ldc_I4_0 );
            __instance.Emit( OpCodes.Br, end );
            __instance.MarkLabel( run );
        }

        private static void MarkFilterEnd( ILGenerator __instance, Type exceptionType )
        {
            if ( exceptionType != null || InstrumentCode <= 0 )
            {
                return;
            }

            Label end;
            lock ( pendingFilterEnds )
            {
                if ( !pendingFilterEnds.TryGetValue( __instance, out end ) )
                {
                    return;
                }
                pendingFilterEnds.Remove( __instance );
            }

            __instance.MarkLabel( end );
        }

        private static bool exceptionFiltersPatched;

        private static void PatchExceptionFilters()
        {
            if ( exceptionFiltersPatched )
            {
                return;
            }
            exceptionFiltersPatched = true;

            try
            {
                var doEmit = typeof( Catch ).GetNestedType( "FilterStatement", BindingFlags.NonPublic )?.GetMethod( "DoEmit",
                    BindingFlags.Instance | BindingFlags.NonPublic );
                int writes = doEmit == null ? 0 : PatchProcessor.GetOriginalInstructions( doEmit ).Count( WritesEndfilter );
                if ( writes != 1 )
                {
                    Main.Log( "Mono.CSharp.Catch.FilterStatement.DoEmit writes endfilter " + writes +
                              " times, expected once — " + ExceptionFilterWarning );
                    return;
                }

                var harmony = new Harmony( "scriptcompiler.internal" );
                harmony.Patch( doEmit,
                    transpiler: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "DropEndfilterWrite",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch Mono.CSharp.Catch.FilterStatement.DoEmit (" + e.GetType().Name + ": " +
                          e.Message + ") — " + ExceptionFilterWarning );
            }
        }

        private const string ExceptionFilterWarning =
            "a catch with a when filter fails with InvalidProgramException when its method runs; catch, test and rethrow instead";

        private static bool WritesEndfilter( CodeInstruction instruction )
        {
            return instruction.opcode == OpCodes.Ldsfld && ( instruction.operand as FieldInfo )?.Name == "Endfilter";
        }

        // ILGenerator.BeginCatchBlock(null) writes the filter's endfilter itself, so the one mcs writes first leaves the second on an empty stack, which Mono's JIT refuses.
        private static IEnumerable<CodeInstruction> DropEndfilterWrite( IEnumerable<CodeInstruction> instructions )
        {
            var kept = instructions.ToList();
            kept.RemoveRange( kept.FindIndex( WritesEndfilter ) - 1, 3 );
            return kept;
        }

        private static bool methodEndPadPatched;

        private static void PatchMethodEndPad()
        {
            if ( methodEndPadPatched )
            {
                return;
            }
            methodEndPadPatched = true;

            try
            {
                var labelFixup = typeof( ILGenerator ).GetMethod( "label_fixup",
                    BindingFlags.Instance | BindingFlags.NonPublic );
                if ( labelFixup == null || ILGeneratorLabels == null || ILGeneratorLabelCount == null ||
                     ILGeneratorCodeLength == null || LabelAddress == null )
                {
                    Main.Log( "System.Reflection.Emit.ILGenerator label members not found — " + MethodEndPadWarning );
                    return;
                }

                var harmony = new Harmony( "scriptcompiler.internal" );
                harmony.Patch( labelFixup,
                    prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "PadMethodEnd",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch System.Reflection.Emit.ILGenerator.label_fixup (" +
                          e.GetType().Name + ": " + e.Message + ") — " + MethodEndPadWarning );
            }
        }

        private const string MethodEndPadWarning =
            "a method ending in a try statement whose end is unreachable, such as a try around a loop that never exits, kills the game when it first runs";

        // Reflection.Emit leaves a try to a label just past its handlers and mcs writes no ret after an unreachable end, so the leave targets one past the last byte and Mono's JIT follows garbage from its offset table.
        private static void PadMethodEnd( ILGenerator __instance )
        {
            int end = (int)ILGeneratorCodeLength.GetValue( __instance );
            int count = (int)ILGeneratorLabelCount.GetValue( __instance );
            var labels = ILGeneratorLabels.GetValue( __instance ) as Array;
            for ( int i = 0; i < count; i++ )
            {
                if ( (int)LabelAddress.GetValue( labels.GetValue( i ) ) != end )
                {
                    continue;
                }

                __instance.Emit( OpCodes.Ldnull );
                __instance.Emit( OpCodes.Throw );
                return;
            }
        }

        private static bool scriptMethodBodyGuardPatched;

        internal static string ScriptMethodBodyGuardFailure;

        private static void PatchScriptMethodBodyGuard()
        {
            if ( scriptMethodBodyGuardPatched )
            {
                return;
            }
            scriptMethodBodyGuardPatched = true;

            try
            {
                var harmony = new Harmony( "scriptcompiler.internal" );
                var prefix = new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "RefuseScriptMethodBody",
                    BindingFlags.Static | BindingFlags.NonPublic ) );
                foreach ( var typeName in new[] { "System.Reflection.MonoMethod", "System.Reflection.MonoCMethod" } )
                {
                    var getBody = typeof( MethodBase ).Assembly.GetType( typeName )?.GetMethod( "GetMethodBody",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null );
                    if ( getBody == null )
                    {
                        throw new MissingMethodException( typeName, "GetMethodBody" );
                    }
                    harmony.Patch( getBody, prefix: prefix );
                }
            }
            catch ( Exception e )
            {
                ScriptMethodBodyGuardFailure = e.GetType().Name + ": " + e.Message;
                Main.Log( "Failed to patch MethodBase.GetMethodBody (" + ScriptMethodBodyGuardFailure +
                          ") — code naming Harmony or GetMethodBody is refused, since reading a script-compiled method's body kills the game" );
            }
        }

        // Mono reads the body from the image's method table, which an AssemblyBuilder's image does not have: metadata.c asserts and the process aborts.
        private static void RefuseScriptMethodBody( MethodBase __instance )
        {
            var declaring = __instance.DeclaringType;
            if ( declaring == null || !( declaring.Assembly is AssemblyBuilder ) )
            {
                return;
            }

            throw new NotSupportedException( declaring.FullName + "." + __instance.Name +
                                             " is compiled by the script system (execute_script or execute_code); " +
                                             "reading its body would kill the game, so GetMethodBody is refused and " +
                                             "Harmony cannot patch it" );
        }

        private static readonly Regex MethodBodyReaders =
            new Regex( @"\b(Harmony\w*|PatchProcessor|GetMethodBody|HasMethodBody)\b" );

        internal static string ScriptMethodBodyRefusal( string source )
        {
            if ( ScriptMethodBodyGuardFailure == null )
            {
                return null;
            }

            var reader = MethodBodyReaders.Match( StripCommentsAndStrings( source ) );
            if ( !reader.Success )
            {
                return null;
            }

            return "This code names " + reader.Value + ", and the guard that keeps reading a method body from killing " +
                   "the game could not be installed (" + ScriptMethodBodyGuardFailure + "). A Harmony patch on, or " +
                   "GetMethodBody of, a method compiled by execute_script or execute_code would take the game down, " +
                   "so nothing was compiled or run.";
        }

        private static bool unloadableMembersPatched;

        private static void PatchUnloadableMembers()
        {
            if ( unloadableMembersPatched )
            {
                return;
            }
            unloadableMembersPatched = true;

            try
            {
                var loadMembers = typeof( CSharpParser ).Assembly.GetType( "Mono.CSharp.ImportedTypeDefinition" )?.GetMethod( "LoadMembers",
                    BindingFlags.Instance | BindingFlags.Public );
                int reads = loadMembers == null ? 0 : PatchProcessor.GetOriginalInstructions( loadMembers ).Count( ReadsMembers );
                if ( reads != 1 )
                {
                    Main.Log( "Mono.CSharp.ImportedTypeDefinition.LoadMembers reads Type.GetMembers " + reads +
                              " times, expected once — " + UnloadableMembersWarning );
                    return;
                }

                var harmony = new Harmony( "scriptcompiler.internal" );
                harmony.Patch( loadMembers,
                    transpiler: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "LoadableMembersTranspiler",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch Mono.CSharp.ImportedTypeDefinition.LoadMembers (" + e.GetType().Name + ": " +
                          e.Message + ") — " + UnloadableMembersWarning );
            }
        }

        private const string UnloadableMembersWarning =
            "the first naming of a type with a method whose signature names an absent assembly, such as Newtonsoft.Json.JsonConvert, fails with CS0584, " +
            "and importing a type with a function-pointer signature, such as 0Harmony's MonoMod.Utils.Helpers, ends the game";

        private static bool ReadsMembers( CodeInstruction instruction )
        {
            var method = instruction.operand as MethodInfo;
            return method != null && method.Name == "GetMembers" && method.DeclaringType == typeof( Type );
        }

        private static IEnumerable<CodeInstruction> LoadableMembersTranspiler( IEnumerable<CodeInstruction> instructions )
        {
            var loadableMembers = typeof( ScriptCompiler ).GetMethod( "LoadableMembers", BindingFlags.Static | BindingFlags.NonPublic );
            foreach ( var instruction in instructions )
            {
                if ( ReadsMembers( instruction ) )
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = loadableMembers;
                }

                yield return instruction;
            }
        }

        // Newtonsoft.Json's XNode overloads name System.Xml.Linq, absent from the game; mcs gives up on the whole type at the first such method, and every member of a MethodBase but its name throws.
        private static MemberInfo[] LoadableMembers( Type type, BindingFlags flags )
        {
            return type.GetMembers( flags ).Where( member =>
            {
                string functionPointer = FunctionPointerSignatures.Describe( member );
                if ( functionPointer != null )
                {
                    ReportImportFailure( SafeAssemblyName( type.Assembly ), ( member is PropertyInfo ? "the property " : "the method " ) + type + "." + member.Name,
                        new NotSupportedException( functionPointer + ", and " + FunctionPointerSignatures.Hazard ) );
                    return false;
                }

                try
                {
                    _ = ( member as MethodBase )?.Attributes;
                    return true;
                }
                catch ( Exception e )
                {
                    ReportImportFailure( SafeAssemblyName( type.Assembly ), "the method " + type + "." + member.Name, e );
                    return false;
                }
            } ).ToArray();
        }

        private static bool guardedReflectionPatched;

        private static Dictionary<IntPtr, MethodInfo> guardedReflection;

        private static void PatchGuardedReflection()
        {
            if ( guardedReflectionPatched )
            {
                return;
            }
            guardedReflectionPatched = true;

            try
            {
                MethodInfo methodEmit = typeof( ILGenerator ).GetMethod( "Emit", new[] { typeof( OpCode ), typeof( MethodInfo ) } );
                if ( methodEmit == null || ILGeneratorCode == null || ILGeneratorCodeLength == null )
                {
                    Main.Log( "System.Reflection.Emit.ILGenerator members not found — " + GuardedReflectionWarning );
                    return;
                }

                guardedReflection = GuardedReflectionTargets();
                Harmony harmony = new Harmony( "scriptcompiler.internal" );
                harmony.Patch( methodEmit,
                    prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "GuardReflectionCall",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch System.Reflection.Emit.ILGenerator.Emit(OpCode, MethodInfo) (" +
                          e.GetType().Name + ": " + e.Message + ") — " + GuardedReflectionWarning );
            }
        }

        private const string GuardedReflectionWarning =
            "script code asking a method or property whose signature names an absent assembly, such as Newtonsoft.Json.JsonConvert's XNode overloads, for its parameters or generic shape, or invoking it, kills the game, " +
            "as does reflecting over a member whose signature holds a function-pointer type, such as 0Harmony's MonoMod internals";

        private static readonly Type[] GuardedDeclaringTypes =
            { typeof( object ), typeof( MemberInfo ), typeof( MethodBase ), typeof( MethodInfo ), typeof( ConstructorInfo ), typeof( PropertyInfo ), typeof( System.Attribute ) };

        private static Dictionary<IntPtr, MethodInfo> GuardedReflectionTargets()
        {
            Type culture = typeof( System.Globalization.CultureInfo );
            var targets = new[]
            {
                new { Name = "GetParameters", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsGenericMethod", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_ContainsGenericParameters", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "GetGenericArguments", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "GetIndexParameters", Parameters = Type.EmptyTypes, Guard = "GuardedGetIndexParameters" },
                new { Name = "get_DeclaringType", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_Module", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_Attributes", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_CallingConvention", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "GetMethodImplementationFlags", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsPublic", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsPrivate", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsFamily", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsAssembly", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsFamilyAndAssembly", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsFamilyOrAssembly", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsStatic", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsFinal", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsVirtual", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsHideBySig", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsAbstract", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsSpecialName", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_IsConstructor", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_ReturnType", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_ReturnParameter", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_ReturnTypeCustomAttributes", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "get_PropertyType", Parameters = Type.EmptyTypes, Guard = "ProbeMember" },
                new { Name = "GetMethodBody", Parameters = Type.EmptyTypes, Guard = "ProbeMethodBody" },
                new { Name = "ToString", Parameters = Type.EmptyTypes, Guard = "ProbeToString" },
                new { Name = "Invoke", Parameters = new[] { typeof( object ), typeof( object[] ) }, Guard = "GuardedInvoke" },
                new
                {
                    Name = "Invoke",
                    Parameters = new[] { typeof( object ), typeof( BindingFlags ), typeof( Binder ), typeof( object[] ), culture },
                    Guard = "GuardedInvokeWithBinder"
                },
                new { Name = "Invoke", Parameters = new[] { typeof( object[] ) }, Guard = "GuardedConstructorInvoke" },
                new
                {
                    Name = "Invoke",
                    Parameters = new[] { typeof( BindingFlags ), typeof( Binder ), typeof( object[] ), culture },
                    Guard = "GuardedConstructorInvokeWithBinder"
                },
                new { Name = "MakeGenericMethod", Parameters = new[] { typeof( Type[] ) }, Guard = "GuardedMakeGenericMethod" },
                new { Name = "GetCustomAttributes", Parameters = new[] { typeof( bool ) }, Guard = "GuardedGetCustomAttributes" },
                new { Name = "GetCustomAttributes", Parameters = new[] { typeof( Type ), typeof( bool ) }, Guard = "GuardedGetCustomAttributesOfType" },
                new { Name = "IsDefined", Parameters = new[] { typeof( Type ), typeof( bool ) }, Guard = "GuardedIsDefined" },
                new { Name = "GetCustomAttribute", Parameters = new[] { typeof( MemberInfo ), typeof( Type ) }, Guard = "GuardedAttributeGetCustomAttribute" },
                new
                {
                    Name = "GetCustomAttribute", Parameters = new[] { typeof( MemberInfo ), typeof( Type ), typeof( bool ) },
                    Guard = "GuardedAttributeGetCustomAttributeInherit"
                },
                new { Name = "GetCustomAttributes", Parameters = new[] { typeof( MemberInfo ) }, Guard = "GuardedAttributeGetCustomAttributes" },
                new
                {
                    Name = "GetCustomAttributes", Parameters = new[] { typeof( MemberInfo ), typeof( bool ) },
                    Guard = "GuardedAttributeGetCustomAttributesInherit"
                },
                new
                {
                    Name = "GetCustomAttributes", Parameters = new[] { typeof( MemberInfo ), typeof( Type ) },
                    Guard = "GuardedAttributeGetCustomAttributesOfType"
                },
                new
                {
                    Name = "GetCustomAttributes", Parameters = new[] { typeof( MemberInfo ), typeof( Type ), typeof( bool ) },
                    Guard = "GuardedAttributeGetCustomAttributesOfTypeInherit"
                },
                new { Name = "IsDefined", Parameters = new[] { typeof( MemberInfo ), typeof( Type ) }, Guard = "GuardedAttributeIsDefined" },
                new { Name = "IsDefined", Parameters = new[] { typeof( MemberInfo ), typeof( Type ), typeof( bool ) }, Guard = "GuardedAttributeIsDefinedInherit" }
            };

            Dictionary<IntPtr, MethodInfo> guarded = new Dictionary<IntPtr, MethodInfo>();
            foreach ( var target in targets )
            {
                MethodInfo guard = typeof( ScriptCompiler ).GetMethod( target.Guard, BindingFlags.Static | BindingFlags.Public );
                List<MethodInfo> originals = GuardedDeclaringTypes
                    .Select( type => type.GetMethod( target.Name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly,
                        null, target.Parameters, null ) )
                    .Where( original => original != null )
                    .ToList();
                if ( originals.Count == 0 )
                {
                    Main.Log( "System.Reflection has no " + target.Name + "(" + string.Join( ", ", target.Parameters.Select( p => p.Name ).ToArray() ) + ") on " +
                              string.Join( ", ", GuardedDeclaringTypes.Select( type => type.Name ).ToArray() ) + " — script code calling it is not guarded" );
                }

                foreach ( MethodInfo original in originals )
                {
                    guarded[original.MethodHandle.Value] = guard;
                }
            }

            return guarded;
        }

        private static void GuardReflectionCall( ILGenerator __instance, ref OpCode opcode, ref MethodInfo meth )
        {
            if ( InstrumentCode <= 0 || meth == null || ( opcode != OpCodes.Call && opcode != OpCodes.Callvirt ) )
            {
                return;
            }

            // MethodHandle throws on the script's own MethodBuilders, so the declaring type is checked first.
            if ( Array.IndexOf( GuardedDeclaringTypes, meth.DeclaringType ) < 0 )
            {
                return;
            }

            MethodInfo guard;
            if ( !guardedReflection.TryGetValue( meth.MethodHandle.Value, out guard ) )
            {
                return;
            }

            // A constrained. prefix must be followed by the callvirt it qualifies, so a call through a generic parameter stays unguarded.
            int length = (int)ILGeneratorCodeLength.GetValue( __instance );
            byte[] code = (byte[])ILGeneratorCode.GetValue( __instance );
            if ( length >= 6 && code[length - 6] == 0xFE && code[length - 5] == 0x16 )
            {
                return;
            }

            if ( guard.ReturnType == typeof( void ) )
            {
                __instance.Emit( OpCodes.Dup );
                __instance.Emit( OpCodes.Call, guard );
                return;
            }

            opcode = OpCodes.Call;
            meth = guard;
        }

        // On a method whose signature names an absent assembly, Attributes throws FileNotFoundException, while the guarded members dereference a null signature natively and end the process.
        internal static void ProbeSignature( MethodBase method, bool locals = false )
        {
            RefuseFunctionPointer( FunctionPointerSignatures.Describe( method ) ?? ( locals ? FunctionPointerSignatures.DescribeLocals( method ) : null ) );
            _ = method.Attributes;
        }

        private static void RefuseFunctionPointer( string functionPointer )
        {
            if ( functionPointer != null )
            {
                throw new NotSupportedException( functionPointer + ", and " + FunctionPointerSignatures.Hazard +
                                                 ", so this read is refused. The member's Name and MetadataToken are safe to filter on." );
            }
        }

        public static void ProbeMember( MemberInfo member )
        {
            MethodBase method = member as MethodBase;
            if ( method != null )
            {
                ProbeSignature( method );
                return;
            }

            PropertyInfo property = member as PropertyInfo;
            if ( property == null )
            {
                return;
            }

            RefuseFunctionPointer( FunctionPointerSignatures.Describe( property ) );
            foreach ( MethodInfo accessor in property.GetAccessors( true ) )
            {
                ProbeSignature( accessor );
            }
        }

        public static void ProbeToString( object value )
        {
            ProbeMember( value as MemberInfo );
        }

        private static bool memberToStringPatched;

        private static void PatchMemberToString()
        {
            if ( memberToStringPatched )
            {
                return;
            }
            memberToStringPatched = true;

            Harmony harmony = new Harmony( "scriptcompiler.internal" );
            var targets = new[]
            {
                new { TypeName = "System.Reflection.MonoMethod", Prefix = "GuardMethodToString" },
                new { TypeName = "System.Reflection.MonoCMethod", Prefix = "GuardMethodToString" },
                new { TypeName = "System.Reflection.MonoProperty", Prefix = "GuardPropertyToString" }
            };
            foreach ( var target in targets )
            {
                try
                {
                    MethodInfo toString = typeof( MethodBase ).Assembly.GetType( target.TypeName )?.GetMethod( "ToString",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null );
                    if ( toString == null )
                    {
                        throw new MissingMethodException( target.TypeName, "ToString" );
                    }
                    harmony.Patch( toString,
                        prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( target.Prefix, BindingFlags.Static | BindingFlags.NonPublic ) ) );
                }
                catch ( Exception e )
                {
                    Main.Log( "Failed to patch " + target.TypeName + ".ToString (" + e.GetType().Name + ": " + e.Message + ") — " + MemberToStringWarning );
                }
            }
        }

        private const string MemberToStringWarning =
            "script code formatting a member as a string through library code, such as string.Join, String.Format or string concatenation, kills the game " +
            "for a constructor whose parameters name an absent assembly, a property with no getter whose type or index parameters name one, " +
            "and a member holding a function-pointer type, such as 0Harmony's MonoMod internals, as a method's return type or parameter, a constructor's parameter, " +
            "a property's type, or the index parameter of a property with no getter";

        private sealed class ToStringVerdict
        {
            public readonly IntPtr Handle;
            public readonly Type Refusal;
            public readonly string Reason;

            public ToStringVerdict( IntPtr handle, Type refusal, string reason )
            {
                Handle = handle;
                Refusal = refusal;
                Reason = reason;
            }
        }

        private static volatile ToStringVerdict[] toStringVerdicts = new ToStringVerdict[64];
        private static int toStringVerdictCount;
        private static readonly object toStringVerdictLock = new object();

        [ThreadStatic] private static bool judgingToString;

        // String.Concat, String.Format and string.Join call ToString from mscorlib, which GuardReflectionCall never rewrites.
        // The native method or property pointer keys the verdict, since a member's DeclaringType and Module read its signature.
        private static void GuardMethodToString( MemberInfo __instance, IntPtr ___mhandle )
        {
            GuardToString( __instance, ___mhandle );
        }

        private static void GuardPropertyToString( MemberInfo __instance, IntPtr ___prop )
        {
            GuardToString( __instance, ___prop );
        }

        private static void GuardToString( MemberInfo member, IntPtr handle )
        {
            ToStringVerdict verdict = FindToStringVerdict( toStringVerdicts, handle ) ?? JudgeToString( member, handle );
            if ( verdict.Refusal == null )
            {
                return;
            }

            string message = "Formatting " + ( member.ReflectedType == null ? "" : member.ReflectedType + "." ) + member.Name +
                             " as a string was refused: " + verdict.Reason;
            Exception refusal;
            try
            {
                refusal = (Exception)Activator.CreateInstance( verdict.Refusal, message );
            }
            catch ( MissingMethodException )
            {
                refusal = new NotSupportedException( message );
            }
            throw refusal;
        }

        private static ToStringVerdict JudgeToString( MemberInfo member, IntPtr handle )
        {
            // A script patch on a method the probe calls could format a member again on this thread.
            if ( judgingToString )
            {
                return new ToStringVerdict( handle, null, null );
            }

            ToStringVerdict verdict;
            judgingToString = true;
            try
            {
                ProbeMember( member );
                verdict = new ToStringVerdict( handle, null, null );
            }
            catch ( Exception e )
            {
                FileNotFoundException missing = e as FileNotFoundException;
                verdict = new ToStringVerdict( handle, e.GetType(), missing?.FileName != null
                    ? "its signature names the assembly '" + missing.FileName + "', which this game lacks"
                    : e.Message );
            }
            finally
            {
                judgingToString = false;
            }

            RecordToStringVerdict( verdict );
            return verdict;
        }

        private static ToStringVerdict FindToStringVerdict( ToStringVerdict[] table, IntPtr handle )
        {
            int mask = table.Length - 1;
            for ( int slot = ToStringSlot( handle ) & mask;; slot = ( slot + 1 ) & mask )
            {
                ToStringVerdict verdict = table[slot];
                if ( verdict == null || verdict.Handle == handle )
                {
                    return verdict;
                }
            }
        }

        private static int ToStringSlot( IntPtr handle )
        {
            long value = handle.ToInt64();
            return (int)( value >> 4 ) ^ (int)( value >> 20 );
        }

        private static int FreeToStringSlot( ToStringVerdict[] table, IntPtr handle )
        {
            int mask = table.Length - 1;
            int slot = ToStringSlot( handle ) & mask;
            while ( table[slot] != null )
            {
                slot = ( slot + 1 ) & mask;
            }
            return slot;
        }

        private static void RecordToStringVerdict( ToStringVerdict verdict )
        {
            lock ( toStringVerdictLock )
            {
                ToStringVerdict[] table = toStringVerdicts;
                if ( FindToStringVerdict( table, verdict.Handle ) != null )
                {
                    return;
                }

                if ( ( toStringVerdictCount + 1 ) * 2 > table.Length )
                {
                    ToStringVerdict[] grown = new ToStringVerdict[table.Length * 2];
                    foreach ( ToStringVerdict entry in table )
                    {
                        if ( entry != null )
                        {
                            grown[FreeToStringSlot( grown, entry.Handle )] = entry;
                        }
                    }
                    table = grown;
                }

                int slot = FreeToStringSlot( table, verdict.Handle );
                // Readers take no lock, so the verdict and a grown table's entries must be visible before the slot that publishes them.
                System.Threading.Thread.MemoryBarrier();
                table[slot] = verdict;
                toStringVerdicts = table;
                toStringVerdictCount++;
            }
        }

        public static void ProbeMethodBody( MethodBase method )
        {
            if ( method != null )
            {
                ProbeSignature( method, true );
            }
        }

        public static object GuardedInvoke( MethodBase method, object obj, object[] parameters )
        {
            ProbeMember( method );
            return method.Invoke( obj, parameters );
        }

        public static object GuardedInvokeWithBinder( MethodBase method, object obj, BindingFlags invokeAttr, Binder binder, object[] parameters,
            System.Globalization.CultureInfo culture )
        {
            ProbeMember( method );
            return method.Invoke( obj, invokeAttr, binder, parameters, culture );
        }

        public static object GuardedConstructorInvoke( ConstructorInfo constructor, object[] parameters )
        {
            ProbeMember( constructor );
            return constructor.Invoke( parameters );
        }

        public static object GuardedConstructorInvokeWithBinder( ConstructorInfo constructor, BindingFlags invokeAttr, Binder binder, object[] parameters,
            System.Globalization.CultureInfo culture )
        {
            ProbeMember( constructor );
            return constructor.Invoke( invokeAttr, binder, parameters, culture );
        }

        public static MethodInfo GuardedMakeGenericMethod( MethodInfo method, Type[] typeArguments )
        {
            ProbeMember( method );
            return method.MakeGenericMethod( typeArguments );
        }

        public static ParameterInfo[] GuardedGetIndexParameters( PropertyInfo property )
        {
            ProbeMember( property );
            return property.GetIndexParameters();
        }

        public static object[] GuardedGetCustomAttributes( MemberInfo member, bool inherit )
        {
            ProbeMember( member );
            return member.GetCustomAttributes( inherit );
        }

        public static object[] GuardedGetCustomAttributesOfType( MemberInfo member, Type attributeType, bool inherit )
        {
            ProbeMember( member );
            return member.GetCustomAttributes( attributeType, inherit );
        }

        public static bool GuardedIsDefined( MemberInfo member, Type attributeType, bool inherit )
        {
            ProbeMember( member );
            return member.IsDefined( attributeType, inherit );
        }

        public static System.Attribute GuardedAttributeGetCustomAttribute( MemberInfo element, Type attributeType )
        {
            ProbeMember( element );
            return System.Attribute.GetCustomAttribute( element, attributeType );
        }

        public static System.Attribute GuardedAttributeGetCustomAttributeInherit( MemberInfo element, Type attributeType, bool inherit )
        {
            ProbeMember( element );
            return System.Attribute.GetCustomAttribute( element, attributeType, inherit );
        }

        public static System.Attribute[] GuardedAttributeGetCustomAttributes( MemberInfo element )
        {
            ProbeMember( element );
            return System.Attribute.GetCustomAttributes( element );
        }

        public static System.Attribute[] GuardedAttributeGetCustomAttributesInherit( MemberInfo element, bool inherit )
        {
            ProbeMember( element );
            return System.Attribute.GetCustomAttributes( element, inherit );
        }

        public static System.Attribute[] GuardedAttributeGetCustomAttributesOfType( MemberInfo element, Type attributeType )
        {
            ProbeMember( element );
            return System.Attribute.GetCustomAttributes( element, attributeType );
        }

        public static System.Attribute[] GuardedAttributeGetCustomAttributesOfTypeInherit( MemberInfo element, Type attributeType, bool inherit )
        {
            ProbeMember( element );
            return System.Attribute.GetCustomAttributes( element, attributeType, inherit );
        }

        public static bool GuardedAttributeIsDefined( MemberInfo element, Type attributeType )
        {
            ProbeMember( element );
            return System.Attribute.IsDefined( element, attributeType );
        }

        public static bool GuardedAttributeIsDefinedInherit( MemberInfo element, Type attributeType, bool inherit )
        {
            ProbeMember( element );
            return System.Attribute.IsDefined( element, attributeType, inherit );
        }

        private static bool genericInstanceFieldsPatched;

        private static void PatchGenericInstanceFields()
        {
            if ( genericInstanceFieldsPatched )
            {
                return;
            }
            genericInstanceFieldsPatched = true;

            try
            {
                var initialize = typeof( Type ).Assembly.GetType( "System.Reflection.MonoGenericClass" )?.GetMethod( "initialize",
                    BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null );
                int reads = initialize == null ? 0 : PatchProcessor.GetOriginalInstructions( initialize ).Count( ReadsBuilderFields );
                if ( reads != 1 )
                {
                    Main.Log( "System.Reflection.MonoGenericClass.initialize reads TypeBuilder.GetFields " + reads +
                              " times, expected once — " + GenericInstanceFieldsWarning );
                    return;
                }

                var harmony = new Harmony( "scriptcompiler.internal" );
                harmony.Patch( initialize,
                    transpiler: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "NoInstanceFieldsTranspiler",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch System.Reflection.MonoGenericClass.initialize (" + e.GetType().Name + ": " +
                          e.Message + ") — " + GenericInstanceFieldsWarning );
            }
        }

        private const string GenericInstanceFieldsWarning =
            "reflecting over the fields of an anonymous type, JsonConvert.SerializeObject of one included, kills the game";

        private static bool ReadsBuilderFields( CodeInstruction instruction )
        {
            var method = instruction.operand as MethodInfo;
            return method != null && method.Name == "GetFields" && method.DeclaringType == typeof( TypeBuilder );
        }

        private static IEnumerable<CodeInstruction> NoInstanceFieldsTranspiler( IEnumerable<CodeInstruction> instructions )
        {
            var noInstanceFields = typeof( ScriptCompiler ).GetMethod( "NoInstanceFields", BindingFlags.Static | BindingFlags.NonPublic );
            foreach ( var instruction in instructions )
            {
                if ( ReadsBuilderFields( instruction ) )
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = noInstanceFields;
                }

                yield return instruction;
            }
        }

        // The runtime copies these fields into a list of the instance's own, and mono_field_get_object aborts on any field that is not in it, which GetFields' are not; an empty list skips that check.
        private static FieldInfo[] NoInstanceFields( TypeBuilder builder, BindingFlags flags )
        {
            return new FieldInfo[0];
        }

        private static bool jsonFieldProviderPatched;

        private static void PatchJsonFieldProvider()
        {
            if ( jsonFieldProviderPatched )
            {
                return;
            }
            jsonFieldProviderPatched = true;

            try
            {
                var harmony = new Harmony( "scriptcompiler.internal" );
                harmony.Patch( AccessTools.Method( typeof( Newtonsoft.Json.Serialization.DefaultContractResolver ), "CreateMemberValueProvider" ),
                    prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "ReflectGenericField",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch Newtonsoft.Json's DefaultContractResolver (" + e.GetType().Name + ": " + e.Message +
                          ") — JsonConvert over a generic class declared by a script or execute_code with a public field kills the game" );
            }
        }

        // A DynamicMethod naming a field of a dynamic generic instance aborts the game when it is created (mono_domain_try_type_resolve asserts); reading the field by reflection is safe.
        private static bool ReflectGenericField( MemberInfo member, ref Newtonsoft.Json.Serialization.IValueProvider __result )
        {
            if ( !IsScriptGenericField( member as FieldInfo ) )
            {
                return true;
            }

            __result = new Newtonsoft.Json.Serialization.ReflectionValueProvider( member );
            return false;
        }

        private static bool IsScriptGenericField( FieldInfo field )
        {
            Type declaring = field?.DeclaringType;
            return declaring != null && declaring.IsGenericType && declaring.Assembly is AssemblyBuilder;
        }

        private static bool genericMethodBindingPatched;

        private static void PatchGenericMethodBinding()
        {
            if ( genericMethodBindingPatched )
            {
                return;
            }
            genericMethodBindingPatched = true;

            try
            {
                var emit = typeof( Method ).GetMethod( "Emit", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null );
                if ( emit == null || MethodBuilderBody == null || LabelFixup == null )
                {
                    Main.Log( "Mono.CSharp.Method.Emit or System.Reflection.Emit.MethodBuilder members not found — " + GenericMethodBindingWarning );
                    return;
                }

                var harmony = new Harmony( "scriptcompiler.internal" );
                harmony.Patch( typeof( MethodSpec ).GetMethod( "GetMetaInfo" ),
                    prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "BindGenericMethod",
                        BindingFlags.Static | BindingFlags.NonPublic ) ),
                    postfix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "RestoreBuilderBody",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
                harmony.Patch( emit,
                    prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "SkipEmittedEarly",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch Mono.CSharp.MethodSpec.GetMetaInfo (" + e.GetType().Name + ": " + e.Message +
                          ") — " + GenericMethodBindingWarning );
            }
        }

        private const string GenericMethodBindingWarning =
            "a generic method called from the same compile that declares it fails with \"does not have a method body\" or runs with an empty body";

        private static readonly FieldInfo MethodBuilderBody =
            typeof( MethodBuilder ).GetField( "ilgen", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly MethodInfo LabelFixup =
            typeof( ILGenerator ).GetMethod( "label_fixup", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly HashSet<Method> emittedEarly = new HashSet<Method>();

        private static string genericBindRefusal;

        // Binding a builder's generic instance makes Mono copy the body as it stands into the instance, branches unpatched, and in a Run assembly drop the builder's ILGenerator. Only a MethodBuilder token resolves late, and the JIT inflates it by the caller's context.
        private static bool BindGenericMethod( MethodSpec __instance, ref MethodBase __result, out ILGenerator __state )
        {
            __state = null;

            var typeArguments = __instance.TypeArguments;
            if ( typeArguments == null || typeArguments.Length == 0 )
            {
                return true;
            }

            var builder = __instance.MemberDefinition.Metadata as MethodBuilder;
            if ( builder == null )
            {
                return true;
            }

            if ( NamesCallersOwnParameters( __instance, builder, typeArguments ) )
            {
                __result = builder;
                return false;
            }

            // The instance's copy of a virtual method has no vtable slot, and a callvirt on it aborts the game when it JITs (class.c asserts method->slot != -1).
            if ( builder.IsVirtual && genericBindRefusal == null )
            {
                genericBindRefusal = VirtualBindRefusal( builder );
            }

            var method = __instance.MemberDefinition as Method;
            if ( method != null && method.Block != null && MethodBuilderBody.GetValue( builder ) == null )
            {
                method.Emit();
                emittedEarly.Add( method );
            }

            __state = (ILGenerator)MethodBuilderBody.GetValue( builder );
            if ( __state == null )
            {
                return true;
            }

            if ( method == null || method.Block == null )
            {
                LabelFixup.Invoke( __state, null );
            }
            else if ( genericBindRefusal == null )
            {
                genericBindRefusal = OwnBodyBindRefusal( builder );
            }

            return true;
        }

        private static bool NamesCallersOwnParameters( MethodSpec spec, MethodBuilder builder, TypeSpec[] typeArguments )
        {
            var classArguments = spec.DeclaringType.TypeArguments;
            for ( int i = 0; i < classArguments.Length; i++ )
            {
                var parameter = classArguments[i].GetMetaInfo() as GenericTypeParameterBuilder;
                if ( parameter == null || parameter.DeclaringMethod != null || parameter.GenericParameterPosition != i ||
                     parameter.DeclaringType != builder.DeclaringType )
                {
                    return false;
                }
            }

            MethodBase caller = null;
            for ( int i = 0; i < typeArguments.Length; i++ )
            {
                var parameter = typeArguments[i].GetMetaInfo() as GenericTypeParameterBuilder;
                if ( parameter == null || parameter.DeclaringMethod == null || parameter.GenericParameterPosition != i ||
                     ( caller != null && parameter.DeclaringMethod != caller ) )
                {
                    return false;
                }
                caller = parameter.DeclaringMethod;
            }

            return caller.GetGenericArguments().Length == typeArguments.Length;
        }

        private static void RestoreBuilderBody( MethodSpec __instance, ILGenerator __state )
        {
            if ( __state != null )
            {
                MethodBuilderBody.SetValue( __instance.MemberDefinition.Metadata, __state );
            }
        }

        private static bool SkipEmittedEarly( Method __instance )
        {
            return !emittedEarly.Remove( __instance );
        }

        internal static string TakeGenericBindRefusal()
        {
            string refusal = genericBindRefusal;
            genericBindRefusal = null;
            emittedEarly.Clear();
            lock ( pendingFilterEnds )
            {
                pendingFilterEnds.Clear();
            }
            return refusal;
        }

        private static string OwnBodyBindRefusal( MethodBuilder builder )
        {
            return "Generic method " + builder.DeclaringType.FullName + "." + builder.Name +
                   " is called with other type arguments from inside its own body or a method it calls, and this runtime " +
                   "would run that call with the body cut off where it stood (InvalidProgramException: Invalid IL code, " +
                   "when it runs), so nothing was run.\n" +
                   "Workarounds:\n" +
                   "  - Call it with the caller's own type parameters, e.g. F<T>(n - 1) inside F<T>\n" +
                   "  - Move the recursion into a non-generic helper the generic method calls\n" +
                   "  - Move the type parameter onto a generic class, e.g. class C<T> { public static int F(int n) { return C<string>.F(n - 1); } }\n" +
                   "  - Call through a delegate field a later call assigns, e.g. public static Func<int, int> Next;";
        }

        private static string VirtualBindRefusal( MethodBuilder builder )
        {
            return "Generic method " + builder.DeclaringType.FullName + "." + builder.Name +
                   " is virtual or an interface method, and calling it with fixed type arguments from the compile that " +
                   "declares it would kill the game when it first ran, so nothing was run.\n" +
                   "Workarounds:\n" +
                   "  - Call it from inside a generic method with that method's own type parameters, e.g. T Via<T>(IThing i, T x) { return i.M(x); }\n" +
                   "  - Make the call from a later execute_code call, or from another script";
        }

        public static void PatchTokenCheck()
        {
            if ( tokenCheckPatched )
            {
                return;
            }
            var mcsAssembly = typeof( CSharpParser ).Assembly;
            var assemblyDefType = mcsAssembly.GetType( "Mono.CSharp.AssemblyDefinition" );
            var checkMethod = assemblyDefType.GetMethod( "CheckReferencesPublicToken",
                BindingFlags.Instance | BindingFlags.NonPublic );
            if ( checkMethod != null )
            {
                var harmony = new Harmony( "scriptcompiler.internal" );
                // mcs's own AssemblyBuilderExtension.GetReferencedAssemblies() returns null on the Reflection.Emit path, and CheckReferencesPublicToken iterates it unguarded.
                harmony.Patch( checkMethod,
                    prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "SkipTokenCheck",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
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
            if ( extensionMethodCrashPatched )
            {
                return;
            }
            var mcsAssembly = typeof( CSharpParser ).Assembly;

            var rootNsType = mcsAssembly.GetType( "Mono.CSharp.RootNamespace" );
            if ( rootNsType == null )
            {
                return;
            }

            var findMethod = rootNsType.GetMethod( "FindExtensionMethodNamespaces",
                BindingFlags.Instance | BindingFlags.Public );

            if ( findMethod != null )
            {
                var harmony = new Harmony( "scriptcompiler.internal" );
                harmony.Patch( findMethod,
                    prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "SkipExtensionMethodSearch",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
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
            lock ( assemblyImportPatchLock )
            {
                if ( assemblyImportPatched )
                {
                    return;
                }

                assemblyImportPatched = true;

                try
                {
                    var importMethod = typeof( ReflectionImporter ).GetMethod( "ImportAssembly",
                        BindingFlags.Instance | BindingFlags.Public,
                        null, new[] { typeof( Assembly ), typeof( RootNamespace ) }, null );

                    if ( importMethod == null )
                    {
                        Main.Log( "Mono.CSharp.ReflectionImporter.ImportAssembly not found — " + ImportDegradedWarning );
                        return;
                    }

                    var harmony = new Harmony( "scriptcompiler.internal" );
                    harmony.Patch( importMethod,
                        prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "ImportAssemblyPerType",
                            BindingFlags.Static | BindingFlags.NonPublic ) ) );
                }
                catch ( Exception e )
                {
                    Main.Log( "Failed to patch Mono.CSharp.ReflectionImporter.ImportAssembly (" +
                              e.GetType().Name + ": " + e.Message + ") — " + ImportDegradedWarning );
                }
            }
        }

        private const string ImportDegradedWarning =
            "assembly import degraded to whole-assembly mode: any exception while importing one type " +
            "now silently drops the remaining types of its assembly (only a ReflectionTypeLoadException " +
            "from GetTypes() is handled), so scripts may fail with CS0246/CS0234 for types that exist";

        private static bool ImportAssemblyPerType( ReflectionImporter __instance, Assembly assembly,
            RootNamespace targetNamespace )
        {
            var assemblyName = SafeAssemblyName( assembly );

            try
            {
                __instance.GetAssemblyDefinition( assembly );
            }
            catch ( Exception e )
            {
                ReportImportFailure( assemblyName, "assembly metadata", e );
                return false;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch ( ReflectionTypeLoadException e )
            {
                types = e.Types; // Partially populated: unloadable entries are null
            }
            catch ( Exception e )
            {
                ReportImportFailure( assemblyName, "type list", e );
                return false;
            }

            if ( types == null )
            {
                ReportImportFailure( assemblyName, "type list", new TypeLoadException( "no types could be loaded" ) );
                return false;
            }

            // UnityModManager Harmony-patches Assembly.GetTypes() to return an empty array for its own assembly.
            if ( types.Length == 0 )
            {
                types = GetTypesPerModule( assembly );
            }

            var single = new Type[1];
            foreach ( var type in types )
            {
                if ( type == null )
                {
                    continue;
                }
                single[0] = type;
                try
                {
                    __instance.ImportTypes( single, targetNamespace, true );
                }
                catch ( Exception e )
                {
                    ReportImportFailure( assemblyName, SafeTypeName( type ), e );
                }
            }

            return false;
        }

        private static Type[] GetTypesPerModule( Assembly assembly )
        {
            Module[] modules;
            try
            {
                modules = assembly.GetModules( true );
            }
            catch ( Exception )
            {
                return new Type[0];
            }

            var collected = new List<Type>();
            foreach ( var module in modules )
            {
                Type[] moduleTypes;
                try
                {
                    moduleTypes = module.GetTypes();
                }
                catch ( ReflectionTypeLoadException e )
                {
                    moduleTypes = e.Types; // Partially populated: unloadable entries are null
                }
                catch ( Exception )
                {
                    continue;
                }

                if ( moduleTypes == null )
                {
                    continue;
                }

                foreach ( var type in moduleTypes )
                {
                    if ( type != null )
                    {
                        collected.Add( type );
                    }
                }
            }

            return collected.ToArray();
        }

        public static void PatchConstantImport()
        {
            lock ( constantImportPatchLock )
            {
                if ( constantImportPatched )
                {
                    return;
                }

                constantImportPatched = true;

                try
                {
                    var createMethod = typeof( MetadataImporter ).GetMethod( "CreateConstantFromValue",
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof( TypeSpec ), typeof( FieldInfo ) }, null );

                    if ( createMethod == null )
                    {
                        Main.Log( "Mono.CSharp.MetadataImporter.CreateConstantFromValue not found — " +
                                  ConstantImportWarning );
                        return;
                    }

                    var rawCalls = PatchProcessor.GetOriginalInstructions( createMethod )
                        .Count( instruction => instruction.Calls( GetRawConstantValueMethod ) );
                    if ( rawCalls != 1 )
                    {
                        Main.Log( "Mono.CSharp.MetadataImporter.CreateConstantFromValue calls " +
                                  "FieldInfo.GetRawConstantValue " + rawCalls + " times, expected once — " +
                                  ConstantImportWarning );
                        return;
                    }

                    var harmony = new Harmony( "scriptcompiler.internal" );
                    harmony.Patch( createMethod,
                        transpiler: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "ReadConstantValueTranspiler",
                            BindingFlags.Static | BindingFlags.NonPublic ) ) );
                }
                catch ( Exception e )
                {
                    Main.Log( "Failed to patch Mono.CSharp.MetadataImporter.CreateConstantFromValue (" +
                              e.GetType().Name + ": " + e.Message + ") — " + ConstantImportWarning );
                }
            }
        }

        private const string ConstantImportWarning =
            "naming a const field or enum declared by a script or by execute_code will crash the game";

        private static readonly MethodInfo GetRawConstantValueMethod =
            typeof( FieldInfo ).GetMethod( "GetRawConstantValue" );

        private static IEnumerable<CodeInstruction> ReadConstantValueTranspiler(
            IEnumerable<CodeInstruction> instructions )
        {
            var readConstantValue = typeof( ScriptCompiler ).GetMethod( "ReadConstantValue",
                BindingFlags.Static | BindingFlags.NonPublic );
            foreach ( var instruction in instructions )
            {
                if ( instruction.Calls( GetRawConstantValueMethod ) )
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = readConstantValue;
                }

                yield return instruction;
            }
        }

        private static object ReadConstantValue( FieldInfo fi )
        {
            // Mono aborts on GetRawConstantValue for a field of an AssemblyBuilder assembly; GetValue(null) reads it.
            if ( !( fi.DeclaringType.Assembly is AssemblyBuilder ) )
            {
                return fi.GetRawConstantValue();
            }
            var value = MemberPath.ReadField( fi, null );
            if ( value != null && value.GetType().IsEnum )
            {
                value = Convert.ChangeType( value, System.Enum.GetUnderlyingType( value.GetType() ) );
            }
            return value;
        }

        public static void PatchAssemblyLocation()
        {
            lock ( assemblyLocationPatchLock )
            {
                if ( assemblyLocationPatched )
                {
                    return;
                }

                assemblyLocationPatched = true;

                try
                {
                    // mcs reads an imported assembly's Location to word errors, and Assembly.Location throws for an AssemblyBuilder.
                    var locationGetter = typeof( ImportedAssemblyDefinition ).GetProperty( "Location",
                        BindingFlags.Instance | BindingFlags.Public )?.GetGetMethod();

                    if ( locationGetter == null )
                    {
                        Main.Log( "Mono.CSharp.ImportedAssemblyDefinition.Location not found — " + AssemblyLocationWarning );
                        return;
                    }

                    var harmony = new Harmony( "scriptcompiler.internal" );
                    harmony.Patch( locationGetter,
                        prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "DynamicAssemblyLocation",
                            BindingFlags.Static | BindingFlags.NonPublic ) ) );
                }
                catch ( Exception e )
                {
                    Main.Log( "Failed to patch Mono.CSharp.ImportedAssemblyDefinition.Location (" +
                              e.GetType().Name + ": " + e.Message + ") — " + AssemblyLocationWarning );
                }
            }
        }

        private const string AssemblyLocationWarning =
            "a compiler error naming a member of a type declared by a script or by execute_code " +
            "will read as CS0584 (internal compiler error)";

        private static bool DynamicAssemblyLocation( ImportedAssemblyDefinition __instance, ref string __result )
        {
            if ( !( __instance.Assembly is AssemblyBuilder ) )
            {
                return true;
            }

            __result = SafeAssemblyName( __instance.Assembly );
            return false;
        }

        private static bool genericParameterImportPatched;

        private static void PatchGenericParameterImport()
        {
            if ( genericParameterImportPatched )
            {
                return;
            }
            genericParameterImportPatched = true;

            try
            {
                var readerType = typeof( MetadataImporter ).GetNestedType( "AttributesTypeInfoReader",
                    BindingFlags.Public | BindingFlags.NonPublic );
                var createType = readerType == null
                    ? null
                    : typeof( MetadataImporter ).GetMethod( "CreateType", BindingFlags.Instance | BindingFlags.NonPublic, null,
                        new[] { typeof( Type ), typeof( TypeSpec ), readerType, typeof( bool ) }, null );
                if ( createType == null )
                {
                    Main.Log( "Mono.CSharp.MetadataImporter.CreateType(Type, TypeSpec, AttributesTypeInfoReader, bool) not found — " +
                              GenericParameterImportWarning );
                    return;
                }

                var harmony = new Harmony( "scriptcompiler.internal" );
                harmony.Patch( createType,
                    prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "ContainerType",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
                harmony.Patch( typeof( ModuleContainer ).GetMethod( "Define" ),
                    prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "ModuleNotFullyDefined",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
                harmony.Patch( typeof( InflatedTypeSpec ).GetMethod( "CreateMetaInfo", BindingFlags.Instance | BindingFlags.NonPublic ),
                    postfix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "InitialiseGenericStruct",
                        BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch Mono.CSharp.MetadataImporter.CreateType (" + e.GetType().Name + ": " +
                          e.Message + ") — " + GenericParameterImportWarning );
            }
        }

        private const string GenericParameterImportWarning =
            "the members of a generic class declared by a script or by execute_code keep their type parameter when named from elsewhere (CS0031), a class cannot derive from a generic class declared in the same execute_code input, and a local of a generic struct they declared fails with stack is too big";

        // Mono copies a TypeBuilder's generic parameters into its container, so its members name a T that GetGenericArguments never returns, and mcs imports that as a type parameter nothing inflates. GetGenericTypeDefinition of its instances answers the TypeBuilder, not the baked type.
        private static void ContainerType( ref Type type )
        {
            var builder = type as TypeBuilder;
            if ( builder != null )
            {
                if ( builder.IsCreated() )
                {
                    type = builder.CreateType();
                }
                return;
            }

            if ( !type.IsGenericParameter || type.DeclaringMethod != null )
            {
                return;
            }

            var declaring = type.DeclaringType;
            if ( declaring != null && declaring.Assembly is AssemblyBuilder )
            {
                type = declaring.GetGenericArguments()[type.GenericParameterPosition];
            }
        }

        // The Evaluator defines every call in one module, which stays marked defined after the first, so a base type naming a generic class declared in the same input has its constraints read before they exist.
        private static void ModuleNotFullyDefined( ModuleContainer __instance )
        {
            __instance.HasTypesFullyDefined = false;
        }

        // An instance of a baked generic struct has no size until reflection or a box initialises it, and a local of one fails the JIT with "stack is too big".
        private static void InitialiseGenericStruct( Type __result )
        {
            if ( !__result.IsValueType || __result.ContainsGenericParameters )
            {
                return;
            }

            var builder = __result.GetGenericTypeDefinition() as TypeBuilder;
            if ( builder != null && builder.IsCreated() )
            {
                __result.GetFields();
            }
        }

        public static void PatchNestedTypeImport()
        {
            lock ( nestedTypeImportPatchLock )
            {
                if ( nestedTypeImportPatched )
                {
                    return;
                }

                nestedTypeImportPatched = true;

                try
                {
                    var readerType = typeof( MetadataImporter ).GetNestedType( "AttributesTypeInfoReader",
                        BindingFlags.Public | BindingFlags.NonPublic );
                    var createType = readerType == null
                        ? null
                        : typeof( MetadataImporter ).GetMethod( "CreateType",
                            BindingFlags.Instance | BindingFlags.NonPublic, null,
                            new[] { typeof( Type ), readerType, typeof( bool ) }, null );

                    if ( createType == null )
                    {
                        Main.Log( "Mono.CSharp.MetadataImporter.CreateType(Type, AttributesTypeInfoReader, bool) not found — " +
                                  NestedTypeImportWarning );
                        return;
                    }

                    var reads = PatchProcessor.GetOriginalInstructions( createType ).Count( ReadsDeclaringType );
                    if ( reads != 2 )
                    {
                        Main.Log( "Mono.CSharp.MetadataImporter.CreateType reads Type.DeclaringType " + reads +
                                  " times, expected twice — " + NestedTypeImportWarning );
                        return;
                    }

                    var harmony = new Harmony( "scriptcompiler.internal" );
                    harmony.Patch( createType,
                        transpiler: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "OpenDeclaringTypeTranspiler",
                            BindingFlags.Static | BindingFlags.NonPublic ) ) );
                }
                catch ( Exception e )
                {
                    Main.Log( "Failed to patch Mono.CSharp.MetadataImporter.CreateType (" + e.GetType().Name + ": " +
                              e.Message + ") — " + NestedTypeImportWarning );
                }
            }
        }

        private const string NestedTypeImportWarning =
            "foreach over Dictionary.Keys, Dictionary.Values or a SortedDictionary will fail with CS0584 " +
            "(internal compiler error); copy the collection first, e.g. foreach (var v in new List<V>(d.Values))";

        private static bool ReadsDeclaringType( CodeInstruction instruction )
        {
            var method = instruction.operand as MethodInfo;
            return method != null && method.Name == "get_DeclaringType" &&
                   method.DeclaringType != null && method.DeclaringType.IsAssignableFrom( typeof( Type ) );
        }

        private static IEnumerable<CodeInstruction> OpenDeclaringTypeTranspiler(
            IEnumerable<CodeInstruction> instructions )
        {
            var openDeclaringType = typeof( ScriptCompiler ).GetMethod( "OpenDeclaringType",
                BindingFlags.Static | BindingFlags.NonPublic );
            foreach ( var instruction in instructions )
            {
                if ( ReadsDeclaringType( instruction ) )
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = openDeclaringType;
                }

                yield return instruction;
            }
        }

        private static Type OpenDeclaringType( Type type )
        {
            // Unity's Mono answers a nested generic instance's DeclaringType already inflated, and mcs inflates it again and throws.
            var declaring = type.DeclaringType;
            if ( declaring == null || !declaring.IsGenericType || declaring.IsGenericTypeDefinition )
            {
                return declaring;
            }
            return declaring.GetGenericTypeDefinition();
        }

        public static void PatchPropertyDefine()
        {
            lock ( propertyDefinePatchLock )
            {
                if ( propertyDefinePatched )
                {
                    return;
                }

                propertyDefinePatched = true;

                try
                {
                    var defineBuilders = typeof( PropertyBase ).GetMethod( "DefineBuilders",
                        BindingFlags.Instance | BindingFlags.NonPublic, null,
                        new[] { typeof( MemberKind ), typeof( ParametersCompiled ) }, null );
                    if ( defineBuilders == null || PropertyBuilderField == null || CheckMissingAccessorMethod == null )
                    {
                        Main.Log( "Mono.CSharp.PropertyBase members not found — " + PropertyDefineWarning );
                        return;
                    }

                    var harmony = new Harmony( "scriptcompiler.internal" );
                    // DefineBuilders calls a TypeBuilder.DefineProperty overload this mscorlib lacks, and Harmony cannot read a body that cannot JIT.
                    foreach ( var caller in new[] { typeof( Property ), typeof( Indexer ) } )
                    {
                        var define = caller.GetMethod( "Define", BindingFlags.Instance | BindingFlags.Public,
                            null, Type.EmptyTypes, null );
                        int calls = define == null
                            ? 0
                            : PatchProcessor.GetOriginalInstructions( define ).Count( i => i.Calls( defineBuilders ) );
                        if ( calls != 1 )
                        {
                            Main.Log( "Mono.CSharp." + caller.Name + ".Define calls DefineBuilders " + calls +
                                      " times, expected once — " + PropertyDefineWarning );
                            continue;
                        }

                        harmony.Patch( define,
                            transpiler: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "RedirectDefineBuilders",
                                BindingFlags.Static | BindingFlags.NonPublic ) ) );
                    }
                }
                catch ( Exception e )
                {
                    Main.Log( "Failed to patch Mono.CSharp property definition (" + e.GetType().Name + ": " +
                              e.Message + ") — " + PropertyDefineWarning );
                }
            }
        }

        private const string PropertyDefineWarning =
            "properties, anonymous types and iterators will fail with MissingMethodException: TypeBuilder.DefineProperty";

        private static readonly FieldInfo PropertyBuilderField =
            typeof( PropertyBase ).GetField( "PropertyBuilder", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly MethodInfo CheckMissingAccessorMethod =
            typeof( PropertyBase ).GetMethod( "CheckMissingAccessor", BindingFlags.Instance | BindingFlags.NonPublic );

        private static IEnumerable<CodeInstruction> RedirectDefineBuilders( IEnumerable<CodeInstruction> instructions )
        {
            var defineBuilders = typeof( PropertyBase ).GetMethod( "DefineBuilders",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof( MemberKind ), typeof( ParametersCompiled ) }, null );
            var replacement = typeof( ScriptCompiler ).GetMethod( "DefinePropertyBuilders",
                BindingFlags.Static | BindingFlags.NonPublic );
            foreach ( var instruction in instructions )
            {
                if ( instruction.Calls( defineBuilders ) )
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }

                yield return instruction;
            }
        }

        private static void DefinePropertyBuilders( PropertyBase property, MemberKind kind, ParametersCompiled parameters )
        {
            var parent = property.Parent;
            // Mono encodes no calling convention in a property signature, so this overload loses nothing.
            var builder = parent.TypeBuilder.DefineProperty(
                property.GetFullName( property.MemberName ), PropertyAttributes.None,
                property.MemberType.GetMetaInfo(), null, null,
                parameters.GetMetaInfo(), null, null );
            PropertyBuilderField.SetValue( property, builder );

            PropertySpec spec = kind == MemberKind.Indexer
                ? new IndexerSpec( parent.Definition, property, property.MemberType, parameters, builder, property.ModFlags )
                : new PropertySpec( kind, parent.Definition, property, property.MemberType, builder, property.ModFlags );

            if ( property.Get != null )
            {
                spec.Get = property.Get.Spec;
                parent.MemberCache.AddMember( property, property.Get.Spec.Name, property.Get.Spec );
            }
            else
            {
                CheckMissingAccessorMethod.Invoke( property, new object[] { kind, parameters, true } );
            }

            if ( property.Set != null )
            {
                spec.Set = property.Set.Spec;
                parent.MemberCache.AddMember( property, property.Set.Spec.Name, property.Set.Spec );
            }
            else
            {
                CheckMissingAccessorMethod.Invoke( property, new object[] { kind, parameters, false } );
            }

            parent.MemberCache.AddMember( property, builder.Name, spec );
        }

        public static void PatchForeignBuilderTokens()
        {
            lock ( foreignBuilderTokensPatchLock )
            {
                if ( foreignBuilderTokensPatched )
                {
                    return;
                }

                foreignBuilderTokensPatched = true;

                try
                {
                    var fieldEmit = typeof( ILGenerator ).GetMethod( "Emit", new[] { typeof( OpCode ), typeof( FieldInfo ) } );
                    var typeEmit = typeof( ILGenerator ).GetMethod( "Emit", new[] { typeof( OpCode ), typeof( Type ) } );
                    if ( fieldEmit == null || typeEmit == null || ILGeneratorModule == null )
                    {
                        Main.Log( "System.Reflection.Emit.ILGenerator members not found — " + ForeignBuilderWarning );
                        return;
                    }

                    if ( ILGeneratorTokenGen == null || DynamicMethodTokenGeneratorType == null )
                    {
                        Main.Log( "System.Reflection.Emit.ILGenerator.token_gen or DynamicMethodTokenGenerator not found — " +
                                  "a DynamicMethod naming a field of a generic class a script or execute_code declared kills the game when it is created" );
                    }

                    // Mono's mono_image_create_token gives another module's FieldBuilder or TypeBuilder a token of the emitting module.
                    var harmony = new Harmony( "scriptcompiler.internal" );
                    harmony.Patch( fieldEmit,
                        prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "ForeignFieldToken",
                            BindingFlags.Static | BindingFlags.NonPublic ) ) );
                    harmony.Patch( typeEmit,
                        prefix: new HarmonyMethod( typeof( ScriptCompiler ).GetMethod( "ForeignTypeToken",
                            BindingFlags.Static | BindingFlags.NonPublic ) ) );
                }
                catch ( Exception e )
                {
                    Main.Log( "Failed to patch System.Reflection.Emit.ILGenerator.Emit (" + e.GetType().Name + ": " +
                              e.Message + ") — " + ForeignBuilderWarning );
                }
            }
        }

        private const string ForeignBuilderWarning =
            "execute_code naming a static of a type an earlier execute_code declared may read or write the wrong field, " +
            "and a DynamicMethod naming a field of a generic class a script or execute_code declared kills the game when it is created";

        private static readonly FieldInfo ILGeneratorModule =
            typeof( ILGenerator ).GetField( "module", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly FieldInfo ILGeneratorTokenGen =
            typeof( ILGenerator ).GetField( "token_gen", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly Type DynamicMethodTokenGeneratorType =
            typeof( ILGenerator ).Assembly.GetType( "System.Reflection.Emit.DynamicMethodTokenGenerator" );

        private static string foreignGenericField;

        // An instance of a dynamic generic class made after the class was baked is not marked complete, and a token on its field aborts the game when the method JITs (mono_domain_try_type_resolve asserts).
        private static void RecordForeignGenericField( ILGenerator generator, FieldInfo field )
        {
            var declaring = field.DeclaringType;
            if ( declaring == null || !declaring.IsGenericType || declaring.IsGenericTypeDefinition )
            {
                return;
            }

            var builder = declaring.GetGenericTypeDefinition() as TypeBuilder;
            if ( builder == null || builder.Module == (Module)ILGeneratorModule.GetValue( generator ) )
            {
                return;
            }

            if ( foreignGenericField == null )
            {
                foreignGenericField = declaring.Name.Split( '`' )[0] + "." + field.Name;
            }
        }

        internal static string TakeForeignGenericFieldRefusal()
        {
            string field = foreignGenericField;
            foreignGenericField = null;
            if ( field == null )
            {
                return null;
            }

            return "This code names the field " + field + " of a generic class declared by an earlier script or execute_code call. " +
                   "The compiled code would kill the game when it first ran, so nothing was run.\n" +
                   "Workarounds:\n" +
                   "  - Declare the member as a property, e.g. public T Value { get; set; }\n" +
                   "  - Read and write the field through methods of the class\n" +
                   "  - Read it by reflection: typeof(Box<int>).GetField(\"Value\").GetValue(box)";
        }

        private static Type CreatedForeignType( ILGenerator generator, Type type )
        {
            var builder = type as TypeBuilder;
            if ( builder == null || !builder.IsCreated() || builder.IsGenericTypeDefinition )
            {
                return null;
            }
            if ( builder.Module == (Module)ILGeneratorModule.GetValue( generator ) )
            {
                return null;
            }
            return builder.CreateType();
        }

        // Creating a DynamicMethod that names a field of a dynamic generic instance aborts the game (mono_domain_try_type_resolve asserts).
        private static void RefuseDynamicMethodGenericField( ILGenerator generator, FieldInfo field )
        {
            if ( !IsScriptGenericField( field ) || ILGeneratorTokenGen == null || DynamicMethodTokenGeneratorType == null )
            {
                return;
            }

            object tokenGen = ILGeneratorTokenGen.GetValue( generator );
            if ( tokenGen == null || !DynamicMethodTokenGeneratorType.IsInstanceOfType( tokenGen ) )
            {
                return;
            }

            throw new NotSupportedException(
                "This code puts the field " + field.DeclaringType.Name.Split( '`' )[0] + "." + field.Name +
                " of a generic class declared by a script or execute_code call into a DynamicMethod. " +
                "Creating that method would kill the game, so the field was refused.\n" +
                "Workarounds:\n" +
                "  - Read and write the field by reflection: var f = typeof(Box<int>).GetField(\"Value\"); f.SetValue(box, 5); f.GetValue(box)\n" +
                "  - Declare the member as a property, e.g. public T Value { get; set; }" );
        }

        private static void ForeignFieldToken( ILGenerator __instance, ref FieldInfo field )
        {
            RefuseDynamicMethodGenericField( __instance, field );

            if ( !( field is FieldBuilder ) )
            {
                RecordForeignGenericField( __instance, field );
                return;
            }

            var created = CreatedForeignType( __instance, field.DeclaringType );
            if ( created == null )
            {
                return;
            }

            var runtimeField = created.GetField( field.Name,
                BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic );
            if ( runtimeField != null )
            {
                field = runtimeField;
            }
        }

        private static void ForeignTypeToken( ILGenerator __instance, ref Type cls )
        {
            var created = CreatedForeignType( __instance, cls );
            if ( created != null )
            {
                cls = created;
            }
        }

        internal static string SafeAssemblyName( Assembly assembly )
        {
            try
            {
                return assembly.GetName().Name;
            }
            catch ( Exception )
            {
                return "<unknown assembly>";
            }
        }

        private static string SafeTypeName( Type type )
        {
            try
            {
                return "type '" + type.FullName + "'";
            }
            catch ( Exception )
            {
                return "type '<unknown>'";
            }
        }

        internal static void ReportImportFailure( string assemblyName, string what, Exception e )
        {
            var key = assemblyName + "|" + e.GetType().Name;
            if ( !reportedImportFailures.Add( key ) )
            {
                return;
            }

            Main.Log( "Script compiler could not import " + what + " from assembly '" + assemblyName +
                      "': " + e.GetType().Name + ": " + e.Message +
                      " (further " + e.GetType().Name + "s from this assembly will not be logged)" );
        }

        private static string DetectEnumUsageError( string name, string source )
        {
            bool ownEnum;
            var referenced = FindEnumUsage( source, VisibleDynamicEnums( name ), out ownEnum );
            if ( referenced == null )
            {
                return null;
            }
            if ( !ownEnum )
            {
                return DynamicEnumUsageMessage( referenced );
            }

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

        internal static string DynamicEnumUsageMessage( string enumName )
        {
            return "Enum '" + enumName + "' is defined by a script or by execute_code, and cannot be named from " +
                   "execute_code or from another script.\n" +
                   "A local of its type kills the game while Mono compiles the method, and a script's enum named " +
                   "from execute_code has its members typed int.\n" +
                   "Workarounds:\n" +
                   "  - Use the enum's underlying values instead of naming '" + enumName + "'\n" +
                   "  - Define the enum in a mod DLL or non-script source";
        }

        internal static string FindEnumUsage( string source, IEnumerable<Type> dynamicEnums, out bool ownEnum )
        {
            var clean = StripCommentsAndStrings( source );

            var enumDecls = new List<KeyValuePair<string, int[]>>();
            foreach ( Match m in Regex.Matches( clean, @"\benum\s+(\w+)" ) )
            {
                var enumName = m.Groups[1].Value;
                var braceIdx = clean.IndexOf( '{', m.Index + m.Length );
                if ( braceIdx < 0 )
                {
                    continue;
                }
                int depth = 1;
                int endIdx = braceIdx + 1;
                while ( endIdx < clean.Length && depth > 0 )
                {
                    if ( clean[endIdx] == '{' )
                    {
                        depth++;
                    }
                    else if ( clean[endIdx] == '}' )
                    {
                        depth--;
                    }
                    endIdx++;
                }
                if ( depth != 0 )
                {
                    continue;
                }
                enumDecls.Add( new KeyValuePair<string, int[]>( enumName, new[] { m.Index, endIdx } ) );
            }

            // Mask out declaration regions so the usage check only sees other references
            var sb = new StringBuilder( clean );
            foreach ( var decl in enumDecls )
            {
                int start = decl.Value[0];
                int end = decl.Value[1];
                for ( int i = start; i < end && i < sb.Length; i++ )
                {
                    if ( sb[i] != '\n' )
                    {
                        sb[i] = ' ';
                    }
                }
            }
            var masked = sb.ToString();

            var declaredTypes = new HashSet<string>();
            foreach ( Match m in DeclaredTypeName.Matches( clean ) )
            {
                var declared = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                foreach ( var segment in declared.Split( '.' ) )
                {
                    declaredTypes.Add( segment );
                }
            }

            ownEnum = true;
            foreach ( var decl in enumDecls )
            {
                if ( ReferencesEnum( masked, decl.Key, declaredTypes, true ) )
                {
                    return decl.Key;
                }
            }

            ownEnum = false;
            foreach ( var type in dynamicEnums )
            {
                if ( declaredTypes.Contains( type.Name ) )
                {
                    continue;
                }

                var qualifiers = new HashSet<string>();
                if ( type.DeclaringType != null )
                {
                    qualifiers.Add( type.DeclaringType.Name.Split( '`' )[0] );
                }
                else if ( !string.IsNullOrEmpty( type.Namespace ) )
                {
                    qualifiers.Add( type.Namespace.Split( '.' ).Last() );
                }

                if ( ReferencesEnum( masked, type.Name, qualifiers, type.DeclaringType == null ) )
                {
                    return type.Name;
                }
            }

            return null;
        }

        private static readonly Regex DeclaredTypeName =
            new Regex( @"\b(?:class|struct|interface|enum|namespace)\s+([\w.]+)|\bdelegate\s+[\w.<>\[\],\s]+?\s+(\w+)\s*[<(]" );

        private static bool ReferencesEnum( string cleanSource, string enumName, ICollection<string> qualifiers,
            bool unqualifiedCounts )
        {
            foreach ( Match m in Regex.Matches( cleanSource, @"\b" + Regex.Escape( enumName ) + @"\b" ) )
            {
                int before = m.Index - 1;
                while ( before >= 0 && char.IsWhiteSpace( cleanSource[before] ) )
                {
                    before--;
                }

                if ( before < 0 || cleanSource[before] != '.' )
                {
                    if ( unqualifiedCounts )
                    {
                        return true;
                    }
                    continue;
                }

                int end = before - 1;
                while ( end >= 0 && char.IsWhiteSpace( cleanSource[end] ) )
                {
                    end--;
                }
                int start = end;
                while ( start >= 0 && ( char.IsLetterOrDigit( cleanSource[start] ) || cleanSource[start] == '_' ) )
                {
                    start--;
                }
                if ( end > start && qualifiers.Contains( cleanSource.Substring( start + 1, end - start ) ) )
                {
                    return true;
                }
            }

            return false;
        }

        internal static List<Type> VisibleDynamicEnums( string exceptName )
        {
            var enums = new List<Type>();
            var pending = new Stack<Type>( LatestEvalTypes() );
            foreach ( var assembly in ScriptManager.LoadedScriptAssemblies( exceptName ) )
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch ( ReflectionTypeLoadException e )
                {
                    types = e.Types; // Partially populated: unloadable entries are null
                }
                catch ( Exception )
                {
                    continue;
                }

                if ( types == null )
                {
                    continue;
                }
                foreach ( var type in types )
                {
                    if ( type != null && !type.IsNested )
                    {
                        pending.Push( type );
                    }
                }
            }

            while ( pending.Count > 0 )
            {
                var type = pending.Pop();
                if ( type.IsEnum )
                {
                    enums.Add( type );
                }
                Type[] nested;
                try
                {
                    nested = type.GetNestedTypes( BindingFlags.Public | BindingFlags.NonPublic );
                }
                catch ( Exception )
                {
                    continue;
                }
                foreach ( var inner in nested )
                {
                    pending.Push( inner );
                }
            }

            return enums;
        }

        private static string StripCommentsAndStrings( string source )
        {
            var text = new StringBuilder( source );
            BlankCommentsAndStrings( text, source, 0, source.Length );
            return text.ToString();
        }

        private static void BlankCommentsAndStrings( StringBuilder text, string source, int from, int to )
        {
            int index = from;
            while ( index < to )
            {
                int end;
                if ( source[index] == '/' && index + 1 < to && source[index + 1] == '/' )
                {
                    end = source.IndexOf( '\n', index );
                    if ( end < 0 )
                    {
                        end = source.Length;
                    }
                }
                else if ( source[index] == '/' && index + 1 < to && source[index + 1] == '*' )
                {
                    end = CodeExecutor.SkipBlockComment( source, index );
                }
                else if ( CodeExecutor.StartsLiteral( source, index ) )
                {
                    end = CodeExecutor.SkipLiteral( source, index );
                }
                else
                {
                    index++;
                    continue;
                }

                end = System.Math.Min( end, to );
                for ( int i = index; i < end; i++ )
                {
                    if ( text[i] != '\n' )
                    {
                        text[i] = ' ';
                    }
                }

                if ( source[index] == '$' )
                {
                    var holes = new List<KeyValuePair<int, int>>();
                    CodeExecutor.SkipInterpolated( source, index, holes );
                    foreach ( var hole in holes )
                    {
                        text.Remove( hole.Key + 1, hole.Value - hole.Key - 2 );
                        text.Insert( hole.Key + 1, source.Substring( hole.Key + 1, hole.Value - hole.Key - 2 ) );
                        BlankCommentsAndStrings( text, source, hole.Key + 1, hole.Value - 1 );
                    }
                }

                index = end;
            }
        }

        internal static string PreambleSource( string source )
        {
            var preamble = new StringBuilder();
            foreach ( var ns in PreambleNamespaces )
            {
                preamble.Append( "using " ).Append( ns ).Append( ";\n" );
            }
            var clean = StripCommentsAndStrings( source );
            foreach ( var alias in PreambleAliases )
            {
                var aliasName = alias.Substring( 0, alias.IndexOf( '=' ) ).Trim();
                if ( Regex.IsMatch( clean, @"\busing\s+" + Regex.Escape( aliasName ) + @"\s*=" ) )
                {
                    continue;
                }

                preamble.Append( "using " ).Append( alias ).Append( ";\n" );
            }
            preamble.Append( "#line 1\n" );
            return preamble.ToString();
        }

        public static CompileResult Compile( string name, string source )
        {
            var enumError = DetectEnumUsageError( name, source );
            if ( enumError != null )
            {
                return new CompileResult
                {
                    Success = false,
                    Errors = enumError
                };
            }

            InstallPatches();

            var bodyRefusal = ScriptMethodBodyRefusal( source );
            if ( bodyRefusal != null )
            {
                return new CompileResult
                {
                    Success = false,
                    Errors = bodyRefusal
                };
            }

            var errorOutput = new StringBuilder();
            var reporter = new StreamReportPrinter( new StringWriter( errorOutput ) );

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

            var ctx = new CompilerContext( settings, reporter );
            ctx.Settings.SourceFiles.Clear();

            source = PreambleSource( source ) + source;

            var sourceBytes = Encoding.UTF8.GetBytes( source );
            var fileName = name + ".cs";

            SeekableStreamReader GetFile( SourceFile file )
            {
                return new SeekableStreamReader( new MemoryStream( sourceBytes ), Encoding.UTF8 );
            }

            ctx.Settings.SourceFiles.Add( new SourceFile( fileName, fileName, 0, GetFile ) );

            var savedToplevel = RootContext.ToplevelTypes;
            try
            {
                var container = new ModuleContainer( ctx );
                RootContext.ToplevelTypes = container;
                Location.Initialize( ctx.Settings.SourceFiles );

                var session = new ParserSession { UseJayGlobalArrays = true, LocatedTokens = new LocatedToken[15000] };
                container.EnableRedefinition();

                foreach ( var sourceFile in ctx.Settings.SourceFiles )
                {
                    var stream = sourceFile.GetInputStream( sourceFile );
                    var compilationSource = new CompilationSourceFile( container, sourceFile );
                    compilationSource.EnableRedefinition();
                    container.AddTypeContainer( compilationSource );
                    var parser = new CSharpParser( stream, compilationSource, session );
                    parser.parse();
                }

                var ass = new AssemblyDefinitionDynamic( container, dllName, dllName + ".dll" );
                container.SetDeclaringAssembly( ass );

                var importer = new ReflectionImporter( container, ctx.BuiltinTypes )
                {
                    IgnoreCompilerGeneratedField = true,
                    IgnorePrivateMembers = false
                };
                ass.Importer = importer;

                // DynamicLoader is internal in this mcs.dll, so use reflection
                var mcsAssembly = typeof( CSharpParser ).Assembly;
                var dynamicLoaderType = mcsAssembly.GetType( "Mono.CSharp.DynamicLoader" );
                var loader = Activator.CreateInstance( dynamicLoaderType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                    null, new object[] { importer, ctx }, null );

                ImportAppDomainAssemblies( a => importer.ImportAssembly( a, container.GlobalRootNamespace ) );
                foreach ( var script in ScriptManager.LoadedScriptAssemblies( name ) )
                {
                    try
                    {
                        importer.ImportAssembly( script, container.GlobalRootNamespace );
                    }
                    catch ( Exception e )
                    {
                        ReportImportFailure( SafeAssemblyName( script ), "any type", e );
                    }
                }
                try
                {
                    importer.ImportTypes( LatestEvalTypes(), container.GlobalRootNamespace, true );
                }
                catch ( Exception e )
                {
                    ReportImportFailure( "eval", "execute_code types", e );
                }

                dynamicLoaderType.GetMethod( "LoadReferences",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
                    .Invoke( loader, new object[] { container } );
                ass.Create( AppDomain.CurrentDomain, AssemblyBuilderAccess.RunAndSave );
                container.CreateContainer();
                dynamicLoaderType.GetMethod( "LoadModules",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
                    .Invoke( loader, new object[] { ass, container.GlobalRootNamespace } );
                container.InitializePredefinedTypes();
                container.Define();

                if ( ctx.Report.Errors > 0 )
                {
                    return new CompileResult
                    {
                        Success = false,
                        Errors = errorOutput.ToString()
                    };
                }

                ass.Resolve();
                TakeForeignGenericFieldRefusal();
                TakeGenericBindRefusal();
                InstrumentCode++;
                try
                {
                    ass.Emit();
                }
                finally
                {
                    InstrumentCode--;
                }

                // Method bodies are resolved during Emit; closing the container after an error there fails on the bodyless method.
                if ( ctx.Report.Errors > 0 )
                {
                    return new CompileResult
                    {
                        Success = false,
                        Errors = errorOutput.ToString()
                    };
                }

                var refusal = TakeForeignGenericFieldRefusal() ?? TakeGenericBindRefusal();
                if ( refusal != null )
                {
                    return new CompileResult
                    {
                        Success = false,
                        Errors = refusal
                    };
                }

                container.CloseContainer();
                ass.EmbedResources();

                return new CompileResult
                {
                    Success = true,
                    Assembly = ass.Builder
                };
            }
            catch ( Exception e )
            {
                Main.Log( "Script compiler failure in '" + name + "':\n" + DescribeExceptionChain( e ) );

                return new CompileResult
                {
                    Success = false,
                    Errors = ctx.Report.Errors > 0 ? errorOutput.ToString() : DescribeInternalError( e )
                };
            }
            finally
            {
                RootContext.ToplevelTypes = savedToplevel;
            }
        }

        private const int ExceptionChainLimit = 32;

        internal static List<Exception> ExceptionChain( Exception e )
        {
            var chain = new List<Exception>();
            for ( var current = e; current != null && chain.Count < ExceptionChainLimit; current = current.InnerException )
            {
                chain.Add( current );
            }
            return chain;
        }

        private static readonly Regex InternalErrorLocation = new Regex( @"^\S*\(\d+(,\d+\+?)?\):" );

        internal static string DescribeInternalError( Exception e )
        {
            var chain = ExceptionChain( e );
            var cause = chain[chain.Count - 1];

            var sb = new StringBuilder();
            sb.Append( "Internal compiler error: " ).Append( cause.GetType().FullName ).Append( ": " ).Append( cause.Message );

            for ( int i = 0; i < chain.Count - 1; i++ )
            {
                if ( !( chain[i] is InternalErrorException ) )
                {
                    continue;
                }

                var where = chain[i].Message.Trim();
                if ( where.EndsWith( ":" ) )
                {
                    continue;
                }
                sb.Append( InternalErrorLocation.IsMatch( where ) ? "\n  while compiling " : "\n  " ).Append( where );
            }

            return sb.ToString();
        }

        private static string DescribeExceptionChain( Exception e )
        {
            if ( e == null )
            {
                return "no exception";
            }

            var chain = ExceptionChain( e );
            var innermost = chain[chain.Count - 1];
            var sb = new StringBuilder();
            sb.Append( "Cause: " ).Append( innermost.GetType().FullName ).Append( ": " ).Append( innermost.Message );
            if ( !string.IsNullOrEmpty( innermost.StackTrace ) )
            {
                sb.Append( "\n" ).Append( innermost.StackTrace );
            }

            if ( innermost.InnerException != null )
            {
                sb.Append( "\n... chain truncated at " ).Append( ExceptionChainLimit ).Append( " levels" );
            }

            for ( int i = chain.Count - 2; i >= 0; i-- )
            {
                sb.Append( "\nWrapped by: " ).Append( chain[i].GetType().FullName )
                    .Append( ": " ).Append( chain[i].Message );
                if ( !string.IsNullOrEmpty( chain[i].StackTrace ) )
                {
                    sb.Append( "\n" ).Append( chain[i].StackTrace );
                }
            }

            return sb.ToString();
        }

        private static AssemblyName ParseName( string fullName )
        {
            try
            {
                return new AssemblyName( fullName );
            }
            catch ( Exception )
            {
                return null;
            }
        }

        private static void ImportAppDomainAssemblies( Action<Assembly> import )
        {
            var dedupedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where( a => !( a is AssemblyBuilder ) )
                .Select( a => new { ass = a, name = ParseName( a.FullName ) } )
                .Where( a => a.name != null )
                .GroupBy( a => a.name.Name )
                .Select( g => g.OrderByDescending( a => a.name.Version ).First() );

            foreach ( var ass in dedupedAssemblies )
            {
                if ( StdLib.Contains( ass.name.Name ) )
                {
                    continue;
                }
                try
                {
                    import( ass.ass );
                }
                catch ( Exception e )
                {
                    ReportImportFailure( ass.name.Name, "any type", e );
                }
            }
        }

        private static readonly HashSet<Assembly> refusedEvalAssemblies = new HashSet<Assembly>();

        internal static void LeaveOutEvalAssembly( Assembly assembly )
        {
            refusedEvalAssemblies.Add( assembly );
        }

        private static IEnumerable<Assembly> EvalAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where( a => a is AssemblyBuilder && EvalAssemblyName.IsMatch( SafeAssemblyName( a ) ) &&
                             !refusedEvalAssemblies.Contains( a ) );
        }

        internal static Assembly NewestEvalAssembly()
        {
            Assembly newest = null;
            int newestNumber = -1;
            foreach ( var assembly in EvalAssemblies() )
            {
                int number;
                if ( int.TryParse( EvalAssemblyName.Match( SafeAssemblyName( assembly ) ).Groups[1].Value, out number )
                     && number > newestNumber )
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
            foreach ( var assembly in EvalAssemblies() )
            {
                int number;
                if ( !int.TryParse( EvalAssemblyName.Match( SafeAssemblyName( assembly ) ).Groups[1].Value, out number ) )
                {
                    continue;
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch ( ReflectionTypeLoadException e )
                {
                    types = e.Types;
                }
                catch ( Exception )
                {
                    continue;
                }

                if ( types == null )
                {
                    continue;
                }

                foreach ( var type in types )
                {
                    if ( type == null || type is TypeBuilder || type.IsNested )
                    {
                        continue;
                    }

                    string fullName = type.FullName;
                    if ( fullName == null || fullName.IndexOf( '<' ) >= 0 )
                    {
                        continue;
                    }

                    KeyValuePair<int, Type> held;
                    if ( !latest.TryGetValue( fullName, out held ) || held.Key < number )
                    {
                        latest[fullName] = new KeyValuePair<int, Type>( number, type );
                    }
                }
            }

            return latest.Values.Select( v => v.Value ).ToArray();
        }
    }
}
