using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Mono.CSharp;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public static class CodeExecutor
    {
        private static Evaluator evaluator;
        private static StringBuilder output;
        private static bool initialized;
        private static readonly object initLock = new object();

        private static readonly List<Assembly> scriptAssemblies = new List<Assembly>();

        private static readonly List<string> recordedUsings = new List<string>();

        private static bool compileBlockPatched;
        private static bool compileBlockReached;

        private static bool genericDeclared;

        private static readonly HashSet<Type> importedEvalTypes = new HashSet<Type>();

        private static InternalErrorException lastInternalError;

        internal static void RegisterScriptAssembly( Assembly assembly )
        {
            lock ( initLock )
            {
                scriptAssemblies.Add( assembly );
                GameObjectInspector.InvalidateTypeCache();
                if ( evaluator == null )
                {
                    return;
                }

                try
                {
                    evaluator.ReferenceAssembly( assembly );
                }
                catch ( Exception ex )
                {
                    Main.Log( "Failed to register script assembly with Evaluator: " + ex.Message );
                }
            }
        }

        internal static void UnregisterScriptAssembly( Assembly assembly )
        {
            lock ( initLock )
            {
                if ( !scriptAssemblies.Remove( assembly ) )
                {
                    return;
                }
                GameObjectInspector.InvalidateTypeCache();

                if ( evaluator == null )
                {
                    return;
                }

                // The Evaluator cannot drop a referenced assembly, and a replaced script's types would collide (CS0433).
                initialized = false;
                evaluator = null;
            }
        }

        private const int MainThreadBudgetMs = 9000;

        private const string BlockSuffix = "\n}";

        private const string CaptureOpen = "Unity_Inspector_Mod.CodeExecutor.__Set(";
        private const string CaptureClose = ");";

        private const string MarkerOpen = "Unity_Inspector_Mod.CodeExecutor.__At(";
        private const string MarkerClose = ");";

        private const int StatementTextLimit = 200;

        private const int SerializeDepthLimit = 4;

        private const string EvaluatorDiscardedMarker = "Evaluator was discarded";

        private static object runValue;
        private static bool runValueSet;

        private static int lastStatement = -1;

        private static ConsoleReportPrinter reportPrinter;

        public static void __Set( object value )
        {
            runValue = value;
            runValueSet = true;
        }

        public static void __At( int statement )
        {
            lastStatement = statement;
        }

        // A block, not a lambda wrapper: a lambda in statement position at the Evaluator's top level whose body-declared local a nested lambda captures null-refs in HoistedVariable.GetFieldExpression (initializer position compiles).
        private static string BlockPrefix()
        {
            return "{ " + Marker( 0 ) + "\n";
        }

        private static string Marker( int statement )
        {
            return MarkerOpen + statement + MarkerClose;
        }

        private sealed class Attempt
        {
            public string Label;
            public string Source;

            public int RowOffset;

            public List<Splice> Splices = new List<Splice>();
            public List<StatementInfo> Statements = new List<StatementInfo>();

            public int MaxRow;

            public List<string> Usings;
        }

        private sealed class AttemptOutcome
        {
            public Attempt Attempt;
            public int Order;
            public bool Parsed;
            public int ErrorCount;
            public int FirstRow;
            public int FirstColumn;
            public string Text;

            public bool Refusal;
        }

        private sealed class Insertion
        {
            public int At;
            public int Order;
            public string Text;
        }

        private sealed class StatementInfo
        {
            public int Row;
            public int Column;
            public string Text;
        }

        private static void Initialize()
        {
            lock ( initLock )
            {
                if ( initialized )
                {
                    return;
                }

                try
                {
                    ScriptCompiler.PatchTokenCheck();
                    ScriptCompiler.PatchExtensionMethodCrash();
                    ScriptCompiler.PatchAssemblyImport();
                    ScriptCompiler.PatchConstantImport();
                    ScriptCompiler.PatchAssemblyLocation();
                    ScriptCompiler.PatchNestedTypeImport();
                    ScriptCompiler.PatchPropertyDefine();
                    ScriptCompiler.PatchForeignBuilderTokens();
                    PatchEvaluator();

                    // AppDomain.GetAssemblies() misses game assemblies until a type in them is touched
                    var typesToPreload = new[]
                    {
                        typeof( GameObject ),
                        typeof( LevelSelectionController ),
                        typeof( HeroController ),
                        typeof( GameState ),
                        typeof( GameModeController )
                    };
                    foreach ( var type in typesToPreload )
                    {
                        _ = type.AssemblyQualifiedName;
                    }

                    output = new StringBuilder();

                    var settings = new CompilerSettings
                    {
                        Version = LanguageVersion.Experimental,
                        GenerateDebugInfo = false,
                        StdLib = true,
                        Target = Target.Library,
                        WarningLevel = 0,
                        EnhancedWarnings = false
                    };

                    reportPrinter = new ConsoleReportPrinter( output );
                    var context = new CompilerContext( settings, reportPrinter );

                    evaluator = new Evaluator( context );

                    // InteractiveBase's statics shadow Unity and game names — bare Time.timeScale fails with CS0023.
                    evaluator.InteractiveBaseClass = typeof( object );

                    var stdLib = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
                    {
                        "mscorlib", "System.Core", "System", "System.Xml", "Microsoft.CSharp"
                    };

                    foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
                    {
                        try
                        {
                            string name = assembly.GetName().Name;
                            if ( stdLib.Contains( name ) )
                            {
                                continue;
                            }

                            // AssemblyBuilder.Location throws NotSupportedException on Unity's mscorlib.
                            if ( assembly is System.Reflection.Emit.AssemblyBuilder )
                            {
                                continue;
                            }

                            if ( !string.IsNullOrEmpty( assembly.Location ) )
                            {
                                evaluator.ReferenceAssembly( assembly );
                            }
                        }
                        catch ( Exception ex )
                        {
                            ScriptCompiler.ReportImportFailure(
                                ScriptCompiler.SafeAssemblyName( assembly ), "any type", ex );
                        }
                    }

                    foreach ( var scriptAssembly in scriptAssemblies )
                    {
                        try
                        {
                            evaluator.ReferenceAssembly( scriptAssembly );
                        }
                        catch ( Exception ex )
                        {
                            ScriptCompiler.ReportImportFailure(
                                ScriptCompiler.SafeAssemblyName( scriptAssembly ), "any type", ex );
                        }
                    }

                    try
                    {
                        var evalTypes = ScriptCompiler.LatestEvalTypes();
                        importedEvalTypes.Clear();
                        importedEvalTypes.UnionWith( evalTypes );
                        evaluator.ImportTypes( true, evalTypes );
                    }
                    catch ( Exception ex )
                    {
                        ScriptCompiler.ReportImportFailure( "eval", "execute_code types", ex );
                    }

                    foreach ( var ns in ScriptCompiler.PreambleNamespaces )
                    {
                        evaluator.Run( "using " + ns + ";" );
                    }
                    foreach ( var alias in ScriptCompiler.PreambleAliases )
                    {
                        evaluator.Run( "using " + alias + ";" );
                    }
                    foreach ( var directive in recordedUsings )
                    {
                        try
                        {
                            evaluator.Run( directive );
                        }
                        catch ( Exception ex )
                        {
                            Main.Log( "Could not restore '" + directive + "' in the rebuilt Evaluator: " + ex.Message );
                        }
                    }

                    // The Evaluator resolves nothing until real type-system code has run; 1+1 is not enough
                    evaluator.Run( "var _warmup = new System.Collections.Generic.List<int>();" );
                    evaluator.Run( "_warmup.Add(1);" );
                    evaluator.Run( "var _go = typeof(UnityEngine.GameObject);" );

                    try
                    {
                        evaluator.Run( "var _lsc = typeof(LevelSelectionController);" );
                        evaluator.Run( "var _hc = typeof(HeroController);" );
                        evaluator.Run( "var _gs = typeof(GameState);" );
                    }
                    catch
                    {
                    }

                    initialized = true;
                }
                catch ( Exception ex )
                {
                    Main.Log( $"Failed to initialize CodeExecutor: {ex.Message}" );
                    initialized = false;
                    evaluator = null;
                    throw;
                }
            }
        }

        public static object Execute( string code )
        {
            return ExecuteWithRetry( code, retriesRemaining: 2 );
        }

        private static object ExecuteWithRetry( string code, int retriesRemaining )
        {
            try
            {
                if ( !initialized )
                {
                    Initialize();
                }

                object executionResult = null;
                Exception executionException = null;
                string failedLabel = null;
                bool invokeStarted = false;

                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    try
                    {
                        var activeEvaluator = evaluator;
                        if ( activeEvaluator == null )
                        {
                            lock ( initLock ) { initialized = false; }
                            executionException = new InvalidOperationException(
                                EvaluatorDiscardedMarker + " before the command ran" );
                            return;
                        }

                        // A local of an enum from a runtime-generated assembly kills the game while Mono compiles the method.
                        bool ownEnum;
                        var dynamicEnum = ScriptCompiler.FindEnumUsage( code, ScriptCompiler.DynamicEnumNames(), out ownEnum );
                        if ( dynamicEnum != null )
                        {
                            executionResult = Failure( ScriptCompiler.DynamicEnumUsageMessage( dynamicEnum ) );
                            return;
                        }

                        string input = StripComments( code );
                        CompiledMethod compiled;
                        string messages;
                        Exception thrown;

                        // The Evaluator reads one input as declarations or as statements, never both.
                        int statementsStart = LeadingDeclarationsEnd( input );
                        if ( statementsStart > 0 )
                        {
                            var declarations = DeclarationsAttempt( input.Substring( 0, statementsStart ), MaxRowOf( input ) );
                            var declarationFailure = CompileAttempt( activeEvaluator, declarations, 0,
                                out compiled, out messages, out thrown );
                            if ( thrown != null )
                            {
                                executionException = thrown;
                                failedLabel = declarations.Label;
                                return;
                            }
                            if ( declarationFailure != null )
                            {
                                executionResult = Failure( declarationFailure.Refusal
                                    ? declarationFailure.Text
                                    : DescribeFailure( new List<AttemptOutcome> { declarationFailure }, declarations.Source ) );
                                return;
                            }

                            // Unity's Mono runs a generic method with an empty body when the Evaluator that declared it compiles the call; a rebuilt one imports the baked type.
                            if ( genericDeclared )
                            {
                                DiscardEvaluator();
                                Initialize();
                                activeEvaluator = evaluator;
                            }

                            input = BlankThrough( input, statementsStart );
                        }

                        var failures = new List<AttemptOutcome>();
                        var attempts = BuildAttempts( input );

                        for ( int i = 0; i < attempts.Count; i++ )
                        {
                            var attempt = attempts[i];

                            var failure = CompileAttempt( activeEvaluator, attempt, i, out compiled, out messages, out thrown );

                            if ( thrown != null )
                            {
                                executionException = thrown;
                                failedLabel = attempt.Label;
                                return;
                            }

                            if ( failure != null && failure.Refusal )
                            {
                                executionResult = Failure( failure.Text );
                                return;
                            }

                            if ( failure != null )
                            {
                                failures.Add( failure );
                                continue;
                            }

                            runValue = null;
                            runValueSet = false;

                            if ( compiled == null )
                            {
                                if ( genericDeclared )
                                {
                                    DiscardEvaluator();
                                }
                                executionResult = new
                                {
                                    success = true,
                                    result = (object)null,
                                    resultSet = false,
                                    output = messages
                                };
                                return;
                            }

                            object retval = null;

                            try
                            {
                                invokeStarted = true;
                                lastStatement = -1;
                                compiled.Invoke( ref retval );
                            }
                            catch ( Exception ex )
                            {
                                runValue = null;
                                runValueSet = false;
                                executionResult = UserFault( ex, attempt );
                                return;
                            }

                            bool hasResult = runValueSet;
                            object serialized = null;

                            try
                            {
                                serialized = hasResult ? SerializeResult( runValue ) : null;
                            }
                            catch ( Exception ex )
                            {
                                executionResult = UserFault( ex, attempt );
                                return;
                            }
                            finally
                            {
                                runValue = null;
                                runValueSet = false;
                            }

                            executionResult = new
                            {
                                success = true,
                                result = serialized,
                                resultSet = hasResult,
                                output = messages
                            };
                            return;
                        }

                        executionResult = Failure( DescribeFailure( failures, input ) );
                    }
                    catch ( Exception ex )
                    {
                        executionException = ex;
                    }
                }, MainThreadBudgetMs );

                if ( executionException != null )
                {
                    if ( retriesRemaining > 0 && !invokeStarted && !IsCompilerFailure( executionException ) &&
                         ( executionException.Message.Contains( EvaluatorDiscardedMarker ) ||
                           executionException is TypeLoadException ) )
                    {
                        System.Threading.Thread.Sleep( 300 );
                        return ExecuteWithRetry( code, retriesRemaining: retriesRemaining - 1 );
                    }

                    Main.Log( $"Code execution failed: {executionException.Message}" );

                    // A compiler crash leaves the Evaluator undefined, so it is rebuilt rather than reused
                    if ( !invokeStarted )
                    {
                        lock ( initLock )
                        {
                            initialized = false;
                            evaluator = null;
                        }
                    }

                    var errorMessage = failedLabel != null
                        ? "[" + failedLabel + "] " + DescribeException( executionException )
                        : DescribeException( executionException );

                    var compilerOutput = output != null ? output.ToString() : null;
                    if ( !string.IsNullOrEmpty( compilerOutput ) )
                    {
                        errorMessage += $"\n  Compiler output: {compilerOutput}";
                    }

                    return new
                    {
                        success = false,
                        error = errorMessage,
                        stackTrace = executionException.StackTrace,
                        position = (object)null,
                        statement = (string)null,
                        result = (object)null
                    };
                }

                return executionResult;
            }
            catch ( TimeoutException tex )
            {
                Main.Log( $"Code execution timed out: {tex.Message}" );
                return new
                {
                    success = false,
                    error = "Code execution timed out after " + MainThreadBudgetMs / 1000 + " seconds",
                    stackTrace = (string)null,
                    position = (object)null,
                    statement = (string)null,
                    result = (object)null
                };
            }
            catch ( Exception ex )
            {
                Main.Log( $"Code execution error: {ex.Message}" );
                return new
                {
                    success = false,
                    error = DescribeException( ex ),
                    stackTrace = ex.StackTrace,
                    position = (object)null,
                    statement = (string)null,
                    result = (object)null
                };
            }
        }

        private static object Failure( string error )
        {
            return new
            {
                success = false,
                error = error,
                stackTrace = (string)null,
                position = (object)null,
                statement = (string)null,
                result = (object)null
            };
        }

        private static bool IsCompilerFailure( Exception ex )
        {
            for ( var current = ex; current != null; current = current.InnerException )
            {
                if ( current is InternalErrorException )
                {
                    return true;
                }
            }
            return false;
        }

        private static void PatchEvaluator()
        {
            if ( evaluatorPatched )
            {
                return;
            }
            evaluatorPatched = true;

            var harmony = new HarmonyLib.Harmony( "scriptcompiler.internal" );

            // mcs reaches CompileBlock only for an input that parsed.
            try
            {
                var compileBlock = typeof( Evaluator ).GetMethod( "CompileBlock",
                    BindingFlags.Instance | BindingFlags.NonPublic );
                if ( compileBlock == null )
                {
                    Main.Log( "Mono.CSharp.Evaluator.CompileBlock not found — execute_code compile errors will be " +
                              "reported from the attempt whose first error sits deepest" );
                }
                else
                {
                    harmony.Patch( compileBlock,
                        prefix: new HarmonyLib.HarmonyMethod( typeof( CodeExecutor ).GetMethod( "MarkCompileBlockReached",
                            BindingFlags.Static | BindingFlags.NonPublic ) ) );
                    compileBlockPatched = true;
                }
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch Mono.CSharp.Evaluator.CompileBlock (" + e.GetType().Name + ": " +
                          e.Message + ") — execute_code compile errors will be reported from the attempt whose " +
                          "first error sits deepest" );
            }

            // Undo.AddTypeContainer drops a redeclared type's defined predecessor but not an imported one, on which NamespaceContainer.AddTypeContainer throws.
            try
            {
                var addTypeContainer = typeof( Undo ).GetMethod( "AddTypeContainer",
                    new[] { typeof( TypeContainer ), typeof( TypeDefinition ) } );
                if ( addTypeContainer == null || NamespaceTypes == null || NamespaceCachedTypes == null ||
                     UndoActions == null )
                {
                    Main.Log( "Mono.CSharp.Undo or Namespace members not found — " + RedeclareWarning );
                }
                else
                {
                    harmony.Patch( addTypeContainer,
                        prefix: new HarmonyLib.HarmonyMethod( typeof( CodeExecutor ).GetMethod( "DropImportedPredecessor",
                            BindingFlags.Static | BindingFlags.NonPublic ) ),
                        postfix: new HarmonyLib.HarmonyMethod( typeof( CodeExecutor ).GetMethod( "RestoreImportedOnUndo",
                            BindingFlags.Static | BindingFlags.NonPublic ) ) );
                }
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch Mono.CSharp.Undo.AddTypeContainer (" + e.GetType().Name + ": " +
                          e.Message + ") — " + RedeclareWarning );
            }

            try
            {
                var record = new HarmonyLib.HarmonyMethod( typeof( CodeExecutor ).GetMethod( "RecordInternalError",
                    BindingFlags.Static | BindingFlags.NonPublic ) );
                foreach ( var constructor in typeof( InternalErrorException ).GetConstructors(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic ) )
                {
                    harmony.Patch( constructor, postfix: record );
                }
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch Mono.CSharp.InternalErrorException (" + e.GetType().Name + ": " +
                          e.Message + ") — execute_code CS0584 errors will not name their cause" );
            }
        }

        private static bool evaluatorPatched;

        private const string RedeclareWarning =
            "after an Evaluator rebuild, execute_code cannot declare again a type an earlier call declared";

        private static readonly FieldInfo NamespaceTypes =
            typeof( Namespace ).GetField( "types", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly FieldInfo NamespaceCachedTypes =
            typeof( Namespace ).GetField( "cached_types", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly FieldInfo UndoActions =
            typeof( Undo ).GetField( "undo_actions", BindingFlags.Instance | BindingFlags.NonPublic );

        private static void DropImportedPredecessor( TypeContainer current_container, TypeDefinition tc,
            out List<TypeSpec> __state )
        {
            __state = null;

            var container = current_container as NamespaceContainer;
            if ( container == null || importedEvalTypes.Count == 0 )
            {
                return;
            }

            var ns = container.NS;
            var types = NamespaceTypes.GetValue( ns ) as Dictionary<string, IList<TypeSpec>>;
            IList<TypeSpec> found;
            if ( types == null || !types.TryGetValue( tc.MemberName.Name, out found ) )
            {
                return;
            }

            var kept = new List<TypeSpec>();
            foreach ( var spec in found )
            {
                if ( spec.Arity == tc.MemberName.Arity && spec.MemberDefinition.IsImported &&
                     importedEvalTypes.Contains( spec.GetMetaInfo() ) )
                {
                    if ( __state == null )
                    {
                        __state = new List<TypeSpec>();
                    }
                    __state.Add( spec );
                }
                else
                {
                    kept.Add( spec );
                }
            }

            if ( __state == null )
            {
                return;
            }

            if ( kept.Count == 0 )
            {
                types.Remove( tc.MemberName.Name );
            }
            else
            {
                types[tc.MemberName.Name] = kept;
            }

            var cached = NamespaceCachedTypes.GetValue( ns ) as Dictionary<string, TypeSpec>;
            if ( cached != null )
            {
                cached.Remove( tc.MemberName.Basename );
            }
        }

        private static void RestoreImportedOnUndo( Undo __instance, TypeContainer current_container,
            List<TypeSpec> __state )
        {
            if ( __state == null )
            {
                return;
            }

            var container = (NamespaceContainer)current_container;
            var actions = UndoActions.GetValue( __instance ) as List<Action>;
            if ( actions == null )
            {
                return;
            }

            foreach ( var spec in __state )
            {
                var restored = spec;
                actions.Add( () => container.NS.AddType( container.Module, restored ) );
            }
        }

        private static void RecordInternalError( InternalErrorException __instance )
        {
            lastInternalError = __instance;
        }

        internal static string InternalErrorCause( string reported )
        {
            var last = lastInternalError;
            if ( last == null || reported == null || !reported.EndsWith( last.Message ) )
            {
                return string.Empty;
            }

            var cause = new StringBuilder();
            for ( var inner = last.InnerException; inner != null; inner = inner.InnerException )
            {
                cause.Append( "\n  Cause: " ).Append( inner.GetType().FullName ).Append( ": " ).Append( inner.Message );
            }
            return cause.ToString();
        }

        private static void MarkCompileBlockReached()
        {
            compileBlockReached = true;
        }

        private static AttemptOutcome CompileAttempt( Evaluator activeEvaluator, Attempt attempt, int order,
            out CompiledMethod compiled, out string messages, out Exception thrown )
        {
            reportPrinter.BeginAttempt( attempt.RowOffset, attempt.Splices, attempt.MaxRow );
            compileBlockReached = false;
            genericDeclared = false;
            lastInternalError = null;

            compiled = null;
            thrown = null;
            string partialInput = null;

            try
            {
                partialInput = activeEvaluator.Compile( attempt.Source, out compiled );
            }
            catch ( Exception ex )
            {
                thrown = ex;
            }

            messages = output.ToString();
            if ( thrown != null )
            {
                return null;
            }

            if ( partialInput == null && reportPrinter.ErrorsCount == 0 )
            {
                var declared = attempt.Usings != null ? ScriptCompiler.NewestEvalAssembly() : null;
                var lambdaError = declared != null ? ScriptCompiler.DetectEmptyGenericLambdas( declared ) : null;
                if ( lambdaError != null )
                {
                    return new AttemptOutcome
                    {
                        Attempt = attempt,
                        Order = order,
                        Parsed = true,
                        Refusal = true,
                        Text = lambdaError
                    };
                }

                genericDeclared = declared != null && DeclaresGenericMethod( declared );
                RecordUsings( attempt.Usings );
                return null;
            }

            return new AttemptOutcome
            {
                Attempt = attempt,
                Order = order,
                Parsed = compileBlockPatched && compileBlockReached,
                ErrorCount = reportPrinter.ErrorsCount,
                FirstRow = reportPrinter.FirstErrorRow,
                FirstColumn = reportPrinter.FirstErrorColumn,
                Text = Describe( attempt.Label, messages, partialInput != null ? "incomplete input" : null )
            };
        }

        private static bool DeclaresGenericMethod( Assembly assembly )
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch ( ReflectionTypeLoadException e )
            {
                types = e.Types;
            }

            foreach ( var type in types )
            {
                if ( type == null )
                {
                    continue;
                }
                foreach ( var method in type.GetMethods( BindingFlags.DeclaredOnly | BindingFlags.Static |
                                                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic ) )
                {
                    if ( method.IsGenericMethodDefinition && !method.Name.StartsWith( "<" ) )
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void DiscardEvaluator()
        {
            lock ( initLock )
            {
                initialized = false;
                evaluator = null;
            }
        }

        private static void RecordUsings( List<string> usings )
        {
            if ( usings == null || usings.Count == 0 )
            {
                return;
            }

            lock ( initLock )
            {
                foreach ( var directive in usings )
                {
                    if ( !recordedUsings.Contains( directive ) )
                    {
                        recordedUsings.Add( directive );
                    }
                }
            }
        }

        private static List<Attempt> BuildAttempts( string code )
        {
            var attempts = new List<Attempt>();
            var splits = FindStatementSplits( code );
            int maxRow = MaxRowOf( code );

            attempts.Add( WholeExpression( "as expression", code ) );
            attempts.Add( BuildValueReturningAttempt( code, splits ) );
            attempts.Add( Statements( "as statements", code, splits ) );

            if ( OpensWithDeclaration( code ) )
            {
                attempts.Add( DeclarationsAttempt( code, maxRow ) );
            }

            foreach ( var attempt in attempts )
            {
                attempt.MaxRow = maxRow;
            }

            return attempts;
        }

        private static int MaxRowOf( string code )
        {
            int maxRow = 1;
            for ( int i = 0; i < code.Length; i++ )
            {
                if ( code[i] == '\n' )
                {
                    maxRow++;
                }
            }
            return maxRow;
        }

        private static Attempt DeclarationsAttempt( string code, int maxRow )
        {
            var usings = new List<string>();
            int consumed;
            ScanLeadingDeclarations( code, usings, null, out consumed );

            return new Attempt
            {
                Label = "as declarations",
                Source = code,
                RowOffset = 0,
                // mcs pushes a putback character ahead of interactive statement input, shifting row 1's columns by one.
                Splices = new List<Splice>
                {
                    new Splice { Row = 1, ColumnFrom = 1, Delta = 1 }
                },
                MaxRow = maxRow,
                Usings = usings
            };
        }

        private static Attempt WholeExpression( string label, string code )
        {
            return new Attempt
            {
                Label = label,
                Source = BlockPrefix() + CaptureOpen + "\n" + code + "\n" + CaptureClose + BlockSuffix,
                RowOffset = 2,
                Statements = BuildStatementTable( code, new List<int>() )
            };
        }

        private static Attempt Statements( string label, string code, List<int> splits )
        {
            var splices = new List<Splice>();
            int endRow;
            int endColumn;
            string body = Rewrite( code, code.Length, splits, splices, out endRow, out endColumn );

            // Terse mode supplies a missing `;` only when the whole input failed to parse, and a block always parses.
            if ( NeedsTerminator( code ) )
            {
                body += ";";
            }

            return new Attempt
            {
                Label = label,
                Source = BlockPrefix() + body + BlockSuffix,
                RowOffset = 1,
                Splices = splices,
                Statements = BuildStatementTable( code, splits )
            };
        }

        private static bool NeedsTerminator( string code )
        {
            int last = LastIndexOfNonWhitespace( code );
            if ( last < 0 )
            {
                return false;
            }

            return code[last] != ';';
        }

        private static Attempt BuildValueReturningAttempt( string code, List<int> splits )
        {
            const string Label = "as statements with value";

            int split = splits.Count > 0 ? splits[splits.Count - 1] : -1;

            var tail = new StringBuilder( code.Substring( split + 1 ) );

            int first = SkipTrivia( tail.ToString(), 0 );
            if ( first < tail.Length && StartsWithReturnKeyword( tail, first ) )
            {
                for ( int i = first; i < first + "return".Length; i++ )
                {
                    tail[i] = ' ';
                }
            }

            int last = LastIndexOfNonWhitespace( tail );
            if ( last >= 0 && tail[last] == ';' )
            {
                tail[last] = ' ';
            }

            if ( IndexOfNonWhitespace( tail, 0 ) < 0 )
            {
                return Statements( Label, code, splits );
            }

            if ( split < 0 )
            {
                return new Attempt
                {
                    Label = Label,
                    Source = BlockPrefix() + CaptureOpen + "\n" + tail + "\n" + CaptureClose + BlockSuffix,
                    RowOffset = 2,
                    Statements = BuildStatementTable( code, splits )
                };
            }

            var splices = new List<Splice>();
            int endRow;
            int endColumn;
            string head = Rewrite( code, split + 1, splits, splices, out endRow, out endColumn );

            splices.Add( new Splice
            {
                Row = endRow,
                ColumnFrom = endColumn,
                Delta = CaptureOpen.Length
            } );

            return new Attempt
            {
                Label = Label,
                Source = BlockPrefix() + head + CaptureOpen + tail + "\n" + CaptureClose + BlockSuffix,
                RowOffset = 1,
                Splices = splices,
                Statements = BuildStatementTable( code, splits )
            };
        }

        private static string Rewrite( string code, int limit, List<int> splits,
            List<Splice> splices, out int endRow, out int endColumn )
        {
            var insertions = new List<Insertion>();
            for ( int k = 0; k < splits.Count && splits[k] < limit; k++ )
            {
                insertions.Add( new Insertion { At = splits[k] + 1, Order = 1, Text = Marker( k + 1 ) } );
            }

            var blanked = new HashSet<int>();
            foreach ( var valueReturn in FindValueReturns( code, limit ) )
            {
                int keyword = valueReturn[0];
                int terminator = valueReturn[1];

                insertions.Add( new Insertion { At = keyword, Order = 2, Text = "{" + CaptureOpen } );
                for ( int i = keyword; i < keyword + "return".Length; i++ )
                {
                    blanked.Add( i );
                }

                if ( terminator >= 0 )
                {
                    insertions.Add( new Insertion { At = terminator, Order = 0, Text = CaptureClose.Substring( 0, 1 ) } );
                    insertions.Add( new Insertion { At = terminator + 1, Order = 0, Text = "return;}" } );
                }
                else
                {
                    insertions.Add( new Insertion { At = limit, Order = 0, Text = CaptureClose + "return;}" } );
                }
            }

            insertions.Sort( ( a, b ) => a.At != b.At ? a.At.CompareTo( b.At ) : a.Order.CompareTo( b.Order ) );

            var text = new StringBuilder( code.Length + 64 * ( insertions.Count + 1 ) );

            int row = 1;
            int rowStart = 0;
            int rowDelta = 0;
            int next = 0;

            for ( int i = 0; i <= limit; i++ )
            {
                while ( next < insertions.Count && insertions[next].At == i )
                {
                    var insertion = insertions[next++];
                    splices.Add( new Splice
                    {
                        Row = row,
                        ColumnFrom = i - rowStart + 1 + rowDelta,
                        Delta = insertion.Text.Length
                    } );
                    text.Append( insertion.Text );
                    rowDelta += insertion.Text.Length;
                }

                if ( i == limit )
                {
                    break;
                }

                char c = code[i];
                text.Append( blanked.Contains( i ) ? ' ' : c );

                if ( c == '\n' )
                {
                    row++;
                    rowStart = i + 1;
                    rowDelta = 0;
                }
            }

            endRow = row;
            endColumn = limit - rowStart + 1 + rowDelta;
            return text.ToString();
        }

        private static List<int[]> FindValueReturns( string code, int limit )
        {
            var found = new List<int[]>();
            var braceIsLambda = new List<bool>();
            int lambdaDepth = 0;
            var openParens = new Stack<int>();
            int lastClosedParen = -1;

            int index = 0;
            while ( index < limit )
            {
                char c = code[index];

                if ( c == '"' || c == '\'' ||
                     ( c == '@' && index + 1 < code.Length && code[index + 1] == '"' ) )
                {
                    index = SkipLiteral( code, index );
                    continue;
                }

                if ( c == '/' && index + 1 < code.Length && code[index + 1] == '*' )
                {
                    index = SkipBlockComment( code, index );
                    continue;
                }

                if ( c == '(' )
                {
                    openParens.Push( index );
                }
                else if ( c == ')' )
                {
                    lastClosedParen = openParens.Count > 0 ? openParens.Pop() : -1;
                }
                else if ( c == '{' )
                {
                    bool lambda = OpensLambdaBody( code, index, lastClosedParen );
                    braceIsLambda.Add( lambda );
                    if ( lambda )
                    {
                        lambdaDepth++;
                    }
                }
                else if ( c == '}' )
                {
                    if ( braceIsLambda.Count > 0 )
                    {
                        if ( braceIsLambda[braceIsLambda.Count - 1] )
                        {
                            lambdaDepth--;
                        }
                        braceIsLambda.RemoveAt( braceIsLambda.Count - 1 );
                    }
                }
                else if ( lambdaDepth == 0 && IsKeywordAt( code, index, "return" ) &&
                          !IsKeywordAt( code, PreviousWordStart( code, index ), "yield" ) )
                {
                    int value = SkipTrivia( code, index + "return".Length );
                    if ( value < limit && code[value] != ';' )
                    {
                        int terminator = ReturnTerminator( code, value, limit );
                        if ( terminator != -2 )
                        {
                            found.Add( new[] { index, terminator } );
                        }
                    }

                    index += "return".Length;
                    continue;
                }

                index++;
            }

            return found;
        }

        private static int ReturnTerminator( string code, int from, int limit )
        {
            int depth = 0;
            int index = from;
            while ( index < limit )
            {
                char c = code[index];

                if ( c == '"' || c == '\'' ||
                     ( c == '@' && index + 1 < code.Length && code[index + 1] == '"' ) )
                {
                    index = SkipLiteral( code, index );
                    continue;
                }

                if ( c == '/' && index + 1 < code.Length && code[index + 1] == '*' )
                {
                    index = SkipBlockComment( code, index );
                    continue;
                }

                if ( c == '(' || c == '[' || c == '{' )
                {
                    depth++;
                }
                else if ( c == ')' || c == ']' || c == '}' )
                {
                    if ( --depth < 0 )
                    {
                        return -2;
                    }
                }
                else if ( c == ';' && depth == 0 )
                {
                    return index;
                }

                index++;
            }

            return depth == 0 ? -1 : -2;
        }

        private static bool OpensLambdaBody( string code, int brace, int lastClosedParen )
        {
            int previous = PreviousSignificant( code, brace );
            if ( previous < 0 )
            {
                return false;
            }

            if ( code[previous] == '>' && previous > 0 && code[previous - 1] == '=' )
            {
                return true;
            }

            if ( code[previous] == ')' )
            {
                if ( lastClosedParen < 0 )
                {
                    return false;
                }
                return IsKeywordAt( code, PreviousWordStart( code, lastClosedParen ), "delegate" );
            }

            return IsKeywordAt( code, PreviousWordStart( code, previous + 1 ), "delegate" );
        }

        private static int PreviousSignificant( string code, int before )
        {
            int index = before - 1;
            while ( index >= 0 )
            {
                if ( char.IsWhiteSpace( code[index] ) )
                {
                    index--;
                    continue;
                }

                if ( code[index] == '/' && index > 0 && code[index - 1] == '*' )
                {
                    int open = code.LastIndexOf( "/*", index - 1, StringComparison.Ordinal );
                    if ( open < 0 )
                    {
                        return index;
                    }
                    index = open - 1;
                    continue;
                }

                return index;
            }

            return -1;
        }

        private static int PreviousWordStart( string code, int before )
        {
            int end = PreviousSignificant( code, before );
            if ( end < 0 || !( char.IsLetterOrDigit( code[end] ) || code[end] == '_' ) )
            {
                return -1;
            }

            int start = end;
            while ( start > 0 && ( char.IsLetterOrDigit( code[start - 1] ) || code[start - 1] == '_' ) )
            {
                start--;
            }
            return start;
        }

        private static bool IsKeywordAt( string code, int start, string word )
        {
            if ( start < 0 || !IsWordAt( code, start, word ) )
            {
                return false;
            }
            if ( start == 0 )
            {
                return true;
            }

            char before = code[start - 1];
            return !( char.IsLetterOrDigit( before ) || before == '_' || before == '@' || before == '.' );
        }

        private static int SkipTrivia( string code, int from )
        {
            int index = from;
            while ( index < code.Length )
            {
                if ( char.IsWhiteSpace( code[index] ) )
                {
                    index++;
                    continue;
                }

                if ( code[index] == '/' && index + 1 < code.Length && code[index + 1] == '*' )
                {
                    index = SkipBlockComment( code, index );
                    continue;
                }

                break;
            }

            return index;
        }

        private static int LeadingDeclarationsEnd( string code )
        {
            int consumed;
            int end = ScanLeadingDeclarations( code, null, null, out consumed );
            if ( consumed == 0 || end < 0 || end >= code.Length )
            {
                return -1;
            }
            return end;
        }

        private static int ScanLeadingDeclarations( string code, List<string> usings, StringBuilder bodies, out int consumed )
        {
            consumed = 0;
            int position = SkipTrivia( code, 0 );

            while ( position < code.Length && OpensWithDeclarationAt( code, position ) )
            {
                bool usingDirective = IsWordAt( code, position, "using" );
                if ( usingDirective )
                {
                    int after = SkipTrivia( code, position + "using".Length );
                    if ( after < code.Length && code[after] == '(' )
                    {
                        break;
                    }
                }

                int body;
                int end = DeclarationEnd( code, position, out body );
                if ( bodies != null && body >= 0 )
                {
                    for ( int i = body + 1; i < ( end < 0 ? code.Length : end ); i++ )
                    {
                        if ( bodies[i] != '\n' )
                        {
                            bodies[i] = ' ';
                        }
                    }
                }
                if ( end < 0 )
                {
                    return -1;
                }

                if ( usingDirective && usings != null )
                {
                    usings.Add( code.Substring( position, end - position ).Trim() );
                }

                consumed++;
                position = SkipTrivia( code, end );
            }

            return position;
        }

        private static int DeclarationEnd( string code, int start, out int body )
        {
            int parens = 0;
            int braces = 0;
            body = -1;
            int index = start;

            while ( index < code.Length )
            {
                char c = code[index];

                if ( c == '"' || c == '\'' ||
                     ( c == '@' && index + 1 < code.Length && code[index + 1] == '"' ) )
                {
                    index = SkipLiteral( code, index );
                    continue;
                }

                if ( c == '/' && index + 1 < code.Length && code[index + 1] == '*' )
                {
                    index = SkipBlockComment( code, index );
                    continue;
                }

                if ( c == '(' || c == '[' )
                {
                    parens++;
                }
                else if ( c == ')' || c == ']' )
                {
                    parens--;
                }
                else if ( c == '{' )
                {
                    if ( braces++ == 0 && body < 0 )
                    {
                        body = index;
                    }
                }
                else if ( c == '}' )
                {
                    if ( --braces == 0 )
                    {
                        int after = SkipTrivia( code, index + 1 );
                        return after < code.Length && code[after] == ';' ? after + 1 : index + 1;
                    }
                    if ( braces < 0 )
                    {
                        return -1;
                    }
                }
                else if ( c == ';' && parens == 0 && braces == 0 )
                {
                    return index + 1;
                }

                index++;
            }

            return -1;
        }

        private static string BlankThrough( string code, int end )
        {
            var text = new StringBuilder( code );
            for ( int i = 0; i < end; i++ )
            {
                if ( text[i] != '\n' )
                {
                    text[i] = ' ';
                }
            }
            return text.ToString();
        }

        private static List<StatementInfo> BuildStatementTable( string code, List<int> splits )
        {
            var statements = new List<StatementInfo>();

            int start = IndexOfNonWhitespace( code, 0 );
            if ( start < 0 )
            {
                return statements;
            }

            int row = 1;
            int rowStart = 0;
            int scan = 0;

            for ( int k = 0; k <= splits.Count; k++ )
            {
                while ( scan < start )
                {
                    if ( code[scan] == '\n' )
                    {
                        row++;
                        rowStart = scan + 1;
                    }
                    scan++;
                }

                int end = k < splits.Count ? splits[k] : code.Length - 1;

                string text = end >= start ? code.Substring( start, end - start + 1 ).Trim() : string.Empty;
                if ( text.Length > StatementTextLimit )
                {
                    text = text.Substring( 0, StatementTextLimit ) + "...";
                }

                statements.Add( new StatementInfo
                {
                    Row = row,
                    Column = start - rowStart + 1,
                    Text = text
                } );

                if ( k < splits.Count )
                {
                    int following = IndexOfNonWhitespace( code, splits[k] + 1 );
                    if ( following < 0 )
                    {
                        break;
                    }
                    start = following;
                }
            }

            return statements;
        }

        internal static int MapColumn( int row, int column, List<Splice> splices )
        {
            if ( splices == null )
            {
                return column;
            }

            int mapped = column;

            for ( int i = 0; i < splices.Count; i++ )
            {
                var splice = splices[i];
                if ( splice.Row == row && splice.ColumnFrom <= column )
                {
                    mapped -= System.Math.Min( splice.Delta, column - splice.ColumnFrom );
                }
            }

            return mapped < 1 ? 1 : mapped;
        }

        private static bool OpensWithDeclaration( string code )
        {
            return OpensWithDeclarationAt( code, SkipTrivia( code, 0 ) );
        }

        private static bool OpensWithDeclarationAt( string code, int start )
        {
            if ( start >= code.Length )
            {
                return false;
            }

            if ( code[start] == '[' )
            {
                return true;
            }

            int end = start;
            while ( end < code.Length && ( char.IsLetterOrDigit( code[end] ) || code[end] == '_' ) )
            {
                end++;
            }
            if ( end == start )
            {
                return false;
            }

            switch ( code.Substring( start, end - start ) )
            {
                case "using":
                case "class":
                case "struct":
                case "interface":
                case "enum":
                case "delegate":
                case "namespace":
                case "public":
                case "internal":
                case "static":
                case "abstract":
                case "sealed":
                    return true;
                default:
                    return false;
            }
        }

        private static int IndexOfNonWhitespace( StringBuilder text, int from )
        {
            for ( int i = from; i < text.Length; i++ )
            {
                if ( !char.IsWhiteSpace( text[i] ) )
                {
                    return i;
                }
            }
            return -1;
        }

        private static int IndexOfNonWhitespace( string text, int from )
        {
            for ( int i = from; i < text.Length; i++ )
            {
                if ( !char.IsWhiteSpace( text[i] ) )
                {
                    return i;
                }
            }
            return -1;
        }

        private static int LastIndexOfNonWhitespace( StringBuilder text )
        {
            for ( int i = text.Length - 1; i >= 0; i-- )
            {
                if ( !char.IsWhiteSpace( text[i] ) )
                {
                    return i;
                }
            }
            return -1;
        }

        private static int LastIndexOfNonWhitespace( string text )
        {
            for ( int i = text.Length - 1; i >= 0; i-- )
            {
                if ( !char.IsWhiteSpace( text[i] ) )
                {
                    return i;
                }
            }
            return -1;
        }

        private static string DescribeFailure( List<AttemptOutcome> failures, string code )
        {
            AttemptOutcome chosen = null;
            foreach ( var failure in failures )
            {
                if ( chosen == null )
                {
                    chosen = failure;
                    continue;
                }

                if ( failure.Parsed != chosen.Parsed )
                {
                    if ( failure.Parsed )
                    {
                        chosen = failure;
                    }
                    continue;
                }

                bool better = failure.Parsed
                    ? failure.ErrorCount < chosen.ErrorCount
                    : failure.FirstRow > chosen.FirstRow ||
                      ( failure.FirstRow == chosen.FirstRow && failure.FirstColumn > chosen.FirstColumn );
                if ( better )
                {
                    chosen = failure;
                }
            }

            string text = chosen != null ? chosen.Text : "compilation failed with no diagnostics";

            int consumed;
            var outsideBodies = new StringBuilder( code );
            ScanLeadingDeclarations( code, null, outsideBodies, out consumed );

            if ( LocalFunction.IsMatch( outsideBodies.ToString() ) )
            {
                text += "\n  Local functions are not supported by this compiler; declare a delegate local instead, " +
                        "e.g. Func<int, int> twice = v => v * 2;";
            }

            return text;
        }

        private static readonly System.Text.RegularExpressions.Regex LocalFunction =
            new System.Text.RegularExpressions.Regex(
                @"(?:^|[;{}])\s*(?:(?:public|private|protected|internal|static|unsafe|extern|async)\s+)*" +
                @"(?!(?:return|new|throw|await|else|case|yield|using|lock|fixed|foreach|while|if|for|switch|catch|var)\b)" +
                @"[A-Za-z_][\w.]*(?:<[^;{}()]*>)?(?:\[\])*\??\s+[A-Za-z_]\w*\s*(?:<[^;{}()]*>)?\s*\([^;{}]*\)\s*(?:\{|=>)" );

        private static List<int> FindStatementSplits( string code )
        {
            var splits = new List<int>();

            int lastMeaningful = LastIndexOfNonWhitespace( code );

            int depth = 0;
            int index = 0;

            while ( index < code.Length )
            {
                char c = code[index];

                if ( c == '"' || c == '\'' ||
                     ( c == '@' && index + 1 < code.Length && code[index + 1] == '"' ) )
                {
                    index = SkipLiteral( code, index );
                    continue;
                }

                if ( c == '/' && index + 1 < code.Length && code[index + 1] == '*' )
                {
                    index = SkipBlockComment( code, index );
                    continue;
                }

                if ( c == '(' || c == '[' || c == '{' )
                {
                    depth++;
                }
                else if ( c == ')' || c == ']' )
                {
                    depth--;
                }
                else if ( c == '}' )
                {
                    depth--;
                    if ( depth == 0 && index != lastMeaningful && ClosesStatement( code, index ) &&
                         !ContinuesStatement( code, index ) )
                    {
                        splits.Add( index );
                    }
                }
                else if ( c == ';' && depth == 0 && index != lastMeaningful &&
                          !ContinuesStatement( code, index ) )
                {
                    splits.Add( index );
                }

                index++;
            }

            return splits;
        }

        // Splitting before `else`/`catch`/`finally`/`while` would strand the tail of one statement as its own.
        private static bool ContinuesStatement( string code, int split )
        {
            int i = split + 1;
            while ( i < code.Length )
            {
                if ( char.IsWhiteSpace( code[i] ) )
                {
                    i++;
                    continue;
                }
                if ( code[i] == '/' && i + 1 < code.Length && code[i + 1] == '*' )
                {
                    i = SkipBlockComment( code, i );
                    continue;
                }
                break;
            }
            if ( i >= code.Length )
            {
                return false;
            }

            return IsWordAt( code, i, "else" ) || IsWordAt( code, i, "catch" ) || IsWordAt( code, i, "finally" ) ||
                   IsWordAt( code, i, "while" );
        }

        private static bool IsWordAt( string code, int start, string word )
        {
            if ( start + word.Length > code.Length )
            {
                return false;
            }
            if ( string.CompareOrdinal( code, start, word, 0, word.Length ) != 0 )
            {
                return false;
            }

            int after = start + word.Length;
            if ( after == code.Length )
            {
                return true;
            }

            char next = code[after];
            return !( char.IsLetterOrDigit( next ) || next == '_' );
        }

        // A `}` also closes an initializer, where the expression continues into an indexer, a call, or an operator.
        private static bool ClosesStatement( string code, int brace )
        {
            for ( int i = brace + 1; i < code.Length; i++ )
            {
                char c = code[i];
                if ( char.IsWhiteSpace( c ) )
                {
                    continue;
                }

                if ( ".,)]};:?+-*/%&|^<>=!~[(".IndexOf( c ) >= 0 )
                {
                    return false;
                }

                return !IsWordOperatorAt( code, i, "as" ) && !IsWordOperatorAt( code, i, "is" );
            }

            return false;
        }

        private static bool IsWordOperatorAt( string code, int start, string word )
        {
            if ( start + word.Length > code.Length )
            {
                return false;
            }
            if ( string.CompareOrdinal( code, start, word, 0, word.Length ) != 0 )
            {
                return false;
            }

            int after = start + word.Length;
            if ( after == code.Length )
            {
                return true;
            }

            char next = code[after];
            return char.IsWhiteSpace( next ) || next == '(';
        }

        private static bool StartsWithReturnKeyword( StringBuilder statement, int start )
        {
            const string Keyword = "return";

            if ( start + Keyword.Length > statement.Length )
            {
                return false;
            }

            for ( int i = 0; i < Keyword.Length; i++ )
            {
                if ( statement[start + i] != Keyword[i] )
                {
                    return false;
                }
            }

            if ( start + Keyword.Length == statement.Length )
            {
                return true;
            }

            char next = statement[start + Keyword.Length];
            return !( char.IsLetterOrDigit( next ) || next == '_' );
        }

        private static string Describe( string label, string messages, string note )
        {
            string text = messages == null ? string.Empty : messages.Trim();

            if ( !string.IsNullOrEmpty( note ) )
            {
                text = text.Length == 0 ? note : text + "\n" + note;
            }

            if ( text.Length == 0 )
            {
                text = "compilation failed with no diagnostics";
            }

            return "[" + label + "] " + text.Replace( "\n", "\n  " );
        }

        private static string DescribeException( Exception ex )
        {
            var message = new StringBuilder();
            message.Append( ex.GetType().Name ).Append( ": " ).Append( ex.Message );

            var inner = ex.InnerException;
            while ( inner != null )
            {
                message.Append( "\n  Inner: " ).Append( inner.GetType().Name )
                    .Append( ": " ).Append( inner.Message );
                inner = inner.InnerException;
            }

            return message.ToString();
        }

        private static object UserFault( Exception ex, Attempt attempt )
        {
            var userException = Unwrap( ex );
            string error = DescribeException( userException );

            StatementInfo info = null;
            if ( attempt != null && attempt.Statements != null &&
                 lastStatement >= 0 && lastStatement < attempt.Statements.Count )
            {
                info = attempt.Statements[lastStatement];
            }

            object position = null;
            if ( info != null )
            {
                position = new { line = info.Row, column = info.Column };
                error += "\n  Statement at (" + info.Row + "," + info.Column + "): " + info.Text;
            }

            return new
            {
                success = false,
                error = error,
                stackTrace = userException.StackTrace,
                position = position,
                statement = info != null ? info.Text : null,
                result = (object)null
            };
        }

        private static Exception Unwrap( Exception ex )
        {
            var invocationFailure = ex as TargetInvocationException;
            return invocationFailure != null && invocationFailure.InnerException != null
                ? invocationFailure.InnerException
                : ex;
        }

        // Block comments pass through: the wrapper suffix sits on its own line, out of their reach
        private static string StripComments( string code )
        {
            var stripped = new StringBuilder( code.Length );
            int index = 0;

            while ( index < code.Length )
            {
                char c = code[index];

                if ( c == '/' && index + 1 < code.Length && code[index + 1] == '/' )
                {
                    while ( index < code.Length && code[index] != '\n' && code[index] != '\r' )
                    {
                        index++;
                    }
                    continue;
                }

                int end;

                if ( c == '/' && index + 1 < code.Length && code[index + 1] == '*' )
                {
                    end = SkipBlockComment( code, index );
                }
                else if ( c == '"' || c == '\'' ||
                          ( c == '@' && index + 1 < code.Length && code[index + 1] == '"' ) )
                {
                    end = SkipLiteral( code, index );
                }
                else
                {
                    stripped.Append( c );
                    index++;
                    continue;
                }

                stripped.Append( code, index, end - index );
                index = end;
            }

            return stripped.ToString().TrimEnd();
        }

        private static int SkipLiteral( string code, int start )
        {
            int index = start;

            if ( code[index] == '@' )
            {
                index += 2;
                while ( index < code.Length )
                {
                    if ( code[index] == '"' )
                    {
                        if ( index + 1 < code.Length && code[index + 1] == '"' )
                        {
                            index += 2;
                            continue;
                        }
                        return index + 1;
                    }
                    index++;
                }
                return index;
            }

            char quote = code[index];
            index++;

            while ( index < code.Length )
            {
                char c = code[index];

                if ( c == '\\' )
                {
                    index += 2;
                    continue;
                }

                if ( c == quote )
                {
                    return index + 1;
                }

                if ( c == '\n' )
                {
                    return index;
                }

                index++;
            }

            return index;
        }

        private static int SkipBlockComment( string code, int start )
        {
            int index = start + 2;

            while ( index + 1 < code.Length )
            {
                if ( code[index] == '*' && code[index + 1] == '/' )
                {
                    return index + 2;
                }
                index++;
            }

            return code.Length;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            bool IEqualityComparer<object>.Equals( object x, object y ) { return ReferenceEquals( x, y ); }

            public int GetHashCode( object obj )
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode( obj );
            }
        }

        private static object SerializeResult( object result )
        {
            return SerializeResult( result, 0, new HashSet<object>( ReferenceComparer.Instance ) );
        }

        private static object SerializeResult( object result, int depth, HashSet<object> visiting )
        {
            if ( result == null )
            {
                return null;
            }

            // Unity's Mono turns runaway recursion into a hard crash, not a catchable exception.
            if ( depth >= SerializeDepthLimit )
            {
                return "... depth limit";
            }

            // A destroyed Unity object is not null to C#, and reading its name throws.
            var unityObject = result as UnityEngine.Object;
            if ( !ReferenceEquals( unityObject, null ) && unityObject == null )
            {
                return new
                {
                    type = result.GetType().FullName,
                    destroyed = true
                };
            }

            if ( result is GameObject go )
            {
                return new
                {
                    type = "GameObject",
                    name = go.name,
                    instanceId = go.GetInstanceID(),
                    isActive = go.activeSelf
                };
            }

            if ( result is Component comp )
            {
                return new
                {
                    type = comp.GetType().Name,
                    gameObject = comp.gameObject.name,
                    instanceId = comp.GetInstanceID()
                };
            }

            if ( result is Vector3 v3 )
            {
                return new { x = v3.x, y = v3.y, z = v3.z };
            }

            if ( result is Vector2 v2 )
            {
                return new { x = v2.x, y = v2.y };
            }

            if ( result is System.Collections.IEnumerable enumerable && !( result is string ) )
            {
                if ( !visiting.Add( result ) )
                {
                    return "... circular reference";
                }

                try
                {
                    var list = new List<object>();
                    foreach ( var item in enumerable )
                    {
                        list.Add( SerializeResult( item, depth + 1, visiting ) );
                        if ( list.Count > 100 )
                        {
                            list.Add( "... truncated" );
                            break;
                        }
                    }
                    return list;
                }
                finally
                {
                    visiting.Remove( result );
                }
            }

            if ( result.GetType().IsPrimitive || result is string || result is decimal )
            {
                return result;
            }

            string text;
            try
            {
                text = result.ToString();
            }
            catch ( Exception ex )
            {
                text = "ToString threw: " + ex.GetType().Name;
            }

            return new
            {
                type = result.GetType().FullName,
                value = text
            };
        }
    }

    // A splice is one run of generated text inside a user row: Row is an input row, ColumnFrom a wrapped-source column.
    internal sealed class Splice
    {
        public int Row;
        public int ColumnFrom;
        public int Delta;
    }

    public class ConsoleReportPrinter : ReportPrinter
    {
        private readonly StringBuilder output;

        private List<Splice> splices;

        public int RowOffset;

        public int MaxRow;

        public int FirstErrorRow { get; private set; }
        public int FirstErrorColumn { get; private set; }

        public ConsoleReportPrinter( StringBuilder output )
        {
            this.output = output;
        }

        internal void BeginAttempt( int rowOffset, List<Splice> splices, int maxRow )
        {
            RowOffset = rowOffset;
            this.splices = splices;
            MaxRow = maxRow;
            FirstErrorRow = 0;
            FirstErrorColumn = 0;
            Reset();
            output.Length = 0;
        }

        public override void Print( AbstractMessage msg, bool showFullPath )
        {
            // Without chaining to the base, ErrorsCount never moves and CompileBlock treats an erroring compile as success.
            base.Print( msg, showFullPath );

            var line = new StringBuilder();

            var location = msg.Location;
            if ( !location.IsNull )
            {
                int row = location.Row - RowOffset;
                if ( row < 1 )
                {
                    row = location.Row;
                }

                int column = CodeExecutor.MapColumn( row, location.Column, splices );

                if ( MaxRow > 0 && row > MaxRow )
                {
                    line.Append( "(past end of input): " );
                }
                else
                {
                    if ( !msg.IsWarning && FirstErrorRow == 0 )
                    {
                        FirstErrorRow = row;
                        FirstErrorColumn = column;
                    }

                    line.Append( '(' ).Append( row ).Append( ',' ).Append( column ).Append( "): " );
                }
            }

            line.Append( msg.MessageType ).Append( " CS" ).Append( msg.Code.ToString( "0000" ) )
                .Append( ": " ).Append( msg.Text );

            if ( msg.Code == 584 )
            {
                line.Append( CodeExecutor.InternalErrorCause( msg.Text ) );
            }

            output.AppendLine( line.ToString() );
        }
    }
}
