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

        internal static void RegisterScriptAssembly(Assembly assembly)
        {
            lock (initLock)
            {
                scriptAssemblies.Add(assembly);
                GameObjectInspector.InvalidateTypeCache();
                if (evaluator == null) return;

                try
                {
                    evaluator.ReferenceAssembly(assembly);
                }
                catch (Exception ex)
                {
                    Main.Log("Failed to register script assembly with Evaluator: " + ex.Message);
                }
            }
        }

        // The Evaluator cannot drop a referenced assembly: a replaced script's types would collide (CS0433).
        internal static void UnregisterScriptAssembly(Assembly assembly)
        {
            lock (initLock)
            {
                if (!scriptAssemblies.Remove(assembly)) return;
                GameObjectInspector.InvalidateTypeCache();

                if (evaluator == null) return;

                initialized = false;
                evaluator = null;
            }
        }

        private const int MainThreadBudgetMs = 9000;

        // A block, not a lambda wrapper: a lambda in statement position at the Evaluator's top level whose body-declared local a nested lambda captures null-refs in HoistedVariable.GetFieldExpression (initializer position compiles).
        private const string BlockPrefix = "{\n";
        private const string BlockSuffix = "\n}";

        private const string CaptureOpen = "Unity_Inspector_Mod.CodeExecutor.__Set(";
        private const string CaptureClose = ");";

        private const int SerializeDepthLimit = 4;

        private const string EvaluatorDiscardedMarker = "Evaluator was discarded";

        private static object runValue;
        private static bool runValueSet;

        private static ConsoleReportPrinter reportPrinter;

        public static void __Set(object value)
        {
            runValue = value;
            runValueSet = true;
        }

        private sealed class Attempt
        {
            public string Label;
            public string Source;

            public int RowOffset;

            public int AdjustRow;
            public int ColumnFrom;
            public int ColumnDelta;

            public int MaxRow;
        }

        private sealed class Diagnostic
        {
            public int Row;
            public int Column;
            public int Order;
            public string Text;
        }

        private static void Initialize()
        {
            lock (initLock)
            {
                if (initialized) return;

                try
                {
                    ScriptCompiler.PatchTokenCheck();
                    ScriptCompiler.PatchExtensionMethodCrash();
                    ScriptCompiler.PatchAssemblyImport();

                    // AppDomain.GetAssemblies() misses game assemblies until a type in them is touched
                    var typesToPreload = new[]
                    {
                        typeof(UnityEngine.GameObject),
                        typeof(LevelSelectionController),
                        typeof(HeroController),
                        typeof(GameState),
                        typeof(GameModeController)
                    };
                    foreach (var type in typesToPreload)
                    {
                        _ = type.AssemblyQualifiedName;
                    }

                    output = new StringBuilder();

                    var settings = new CompilerSettings()
                    {
                        Version = LanguageVersion.Experimental,
                        GenerateDebugInfo = false,
                        StdLib = true,
                        Target = Target.Library,
                        WarningLevel = 0,
                        EnhancedWarnings = false
                    };

                    reportPrinter = new ConsoleReportPrinter(output);
                    var context = new CompilerContext(settings, reportPrinter);

                    evaluator = new Evaluator(context);

                    // InteractiveBase's statics shadow Unity and game names — bare Time.timeScale fails with CS0023.
                    evaluator.InteractiveBaseClass = typeof(object);

                    var stdLib = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "mscorlib", "System.Core", "System", "System.Xml", "Microsoft.CSharp"
                    };

                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            string name = assembly.GetName().Name;
                            if (stdLib.Contains(name))
                                continue;

                            // AssemblyBuilder.Location throws NotSupportedException on Unity's mscorlib.
                            if (assembly is System.Reflection.Emit.AssemblyBuilder)
                                continue;

                            if (!string.IsNullOrEmpty(assembly.Location))
                            {
                                evaluator.ReferenceAssembly(assembly);
                            }
                        }
                        catch (Exception ex)
                        {
                            ScriptCompiler.ReportImportFailure(
                                ScriptCompiler.SafeAssemblyName(assembly), "any type", ex);
                        }
                    }

                    foreach (var scriptAssembly in scriptAssemblies)
                    {
                        try
                        {
                            evaluator.ReferenceAssembly(scriptAssembly);
                        }
                        catch (Exception ex)
                        {
                            ScriptCompiler.ReportImportFailure(
                                ScriptCompiler.SafeAssemblyName(scriptAssembly), "any type", ex);
                        }
                    }

                    foreach (var ns in ScriptCompiler.PreambleNamespaces)
                    {
                        evaluator.Run("using " + ns + ";");
                    }

                    // The Evaluator resolves nothing until real type-system code has run; 1+1 is not enough
                    evaluator.Run("var _warmup = new System.Collections.Generic.List<int>();");
                    evaluator.Run("_warmup.Add(1);");
                    evaluator.Run("var _go = typeof(UnityEngine.GameObject);");

                    try
                    {
                        evaluator.Run("var _lsc = typeof(LevelSelectionController);");
                        evaluator.Run("var _hc = typeof(HeroController);");
                        evaluator.Run("var _gs = typeof(GameState);");
                    }
                    catch
                    {
                    }

                    initialized = true;
                }
                catch (Exception ex)
                {
                    Main.Log($"Failed to initialize CodeExecutor: {ex.Message}");
                    initialized = false;
                    evaluator = null;
                    throw;
                }
            }
        }
        
        public static object Execute(string code)
        {
            return ExecuteWithRetry(code, retriesRemaining: 2);
        }

        private static object ExecuteWithRetry(string code, int retriesRemaining)
        {
            try
            {
                if (!initialized)
                {
                    Initialize();
                }

                object executionResult = null;
                Exception executionException = null;
                string failedLabel = null;
                bool invokeStarted = false;

                MainThreadDispatcher.EnqueueAndWait(() =>
                {
                    try
                    {
                        var activeEvaluator = evaluator;
                        if (activeEvaluator == null)
                        {
                            lock (initLock) { initialized = false; }
                            executionException = new InvalidOperationException(
                                EvaluatorDiscardedMarker + " before the command ran");
                            return;
                        }

                        var diagnostics = new List<Diagnostic>();
                        var attempts = BuildAttempts(StripComments(code));

                        for (int i = 0; i < attempts.Count; i++)
                        {
                            var attempt = attempts[i];

                            reportPrinter.BeginAttempt(attempt.RowOffset, attempt.AdjustRow,
                                attempt.ColumnFrom, attempt.ColumnDelta, attempt.MaxRow);

                            CompiledMethod compiled = null;
                            string partialInput = null;
                            Exception compileException = null;

                            try
                            {
                                partialInput = activeEvaluator.Compile(attempt.Source, out compiled);
                            }
                            catch (Exception ex)
                            {
                                compileException = ex;
                            }

                            string messages = output.ToString();

                            if (compileException != null)
                            {
                                executionException = compileException;
                                failedLabel = attempt.Label;
                                return;
                            }

                            if (partialInput != null || reportPrinter.ErrorsCount > 0)
                            {
                                diagnostics.Add(new Diagnostic
                                {
                                    Row = reportPrinter.FirstErrorRow,
                                    Column = reportPrinter.FirstErrorColumn,
                                    Order = i,
                                    Text = Describe(attempt.Label, messages,
                                        partialInput != null ? "incomplete input" : null)
                                });
                                continue;
                            }

                            runValue = null;
                            runValueSet = false;

                            if (compiled == null)
                            {
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
                                compiled.Invoke(ref retval);
                            }
                            catch (Exception ex)
                            {
                                runValue = null;
                                runValueSet = false;
                                executionResult = UserFault(ex);
                                return;
                            }

                            bool hasResult = runValueSet;
                            object serialized = null;

                            try
                            {
                                serialized = hasResult ? SerializeResult(runValue) : null;
                            }
                            catch (Exception ex)
                            {
                                executionResult = UserFault(ex);
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

                        executionResult = new
                        {
                            success = false,
                            error = JoinDiagnostics(diagnostics),
                            stackTrace = (string)null,
                            result = (object)null
                        };
                    }
                    catch (Exception ex)
                    {
                        executionException = ex;
                    }
                }, MainThreadBudgetMs);

                if (executionException != null)
                {
                    if (retriesRemaining > 0 && !invokeStarted &&
                        (executionException.Message.Contains(EvaluatorDiscardedMarker) ||
                         executionException.Message.Contains("InteractiveHost") ||
                         executionException.Message.Contains("Unexpected error when loading type") ||
                         executionException is TypeLoadException))
                    {
                        System.Threading.Thread.Sleep(300);
                        return ExecuteWithRetry(code, retriesRemaining: retriesRemaining - 1);
                    }

                    Main.Log($"Code execution failed: {executionException.Message}");

                    // A compiler crash leaves the Evaluator undefined, so it is rebuilt rather than reused
                    if (!invokeStarted)
                    {
                        lock (initLock)
                        {
                            initialized = false;
                            evaluator = null;
                        }
                    }

                    var errorMessage = failedLabel != null
                        ? "[" + failedLabel + "] " + DescribeException(executionException)
                        : DescribeException(executionException);

                    var compilerOutput = output != null ? output.ToString() : null;
                    if (!string.IsNullOrEmpty(compilerOutput))
                    {
                        errorMessage += $"\n  Compiler output: {compilerOutput}";
                    }

                    return new
                    {
                        success = false,
                        error = errorMessage,
                        stackTrace = executionException.StackTrace,
                        result = (object)null
                    };
                }

                return executionResult;
            }
            catch (TimeoutException tex)
            {
                Main.Log($"Code execution timed out: {tex.Message}");
                return new
                {
                    success = false,
                    error = "Code execution timed out after " + (MainThreadBudgetMs / 1000) + " seconds",
                    stackTrace = (string)null,
                    result = (object)null
                };
            }
            catch (Exception ex)
            {
                Main.Log($"Code execution error: {ex.Message}");
                return new
                {
                    success = false,
                    error = DescribeException(ex),
                    stackTrace = ex.StackTrace,
                    result = (object)null
                };
            }
        }

        private static List<Attempt> BuildAttempts(string code)
        {
            var attempts = new List<Attempt>();

            attempts.Add(WholeExpression("as expression", code));
            attempts.Add(BuildValueReturningAttempt(code));
            attempts.Add(Statements("as statements", code));

            if (OpensWithDeclaration(code))
            {
                attempts.Add(new Attempt
                {
                    Label = "as declarations",
                    Source = code,
                    RowOffset = 0,
                    // mcs pushes a putback character ahead of interactive statement input, shifting row 1's columns by one.
                    AdjustRow = 1,
                    ColumnFrom = 1,
                    ColumnDelta = 1
                });
            }

            int maxRow = 1;
            for (int i = 0; i < code.Length; i++)
            {
                if (code[i] == '\n') maxRow++;
            }
            foreach (var attempt in attempts) attempt.MaxRow = maxRow;

            return attempts;
        }

        private static Attempt WholeExpression(string label, string code)
        {
            return new Attempt
            {
                Label = label,
                Source = BlockPrefix + CaptureOpen + "\n" + code + "\n" + CaptureClose + BlockSuffix,
                RowOffset = 2
            };
        }

        private static Attempt Statements(string label, string code)
        {
            // Terse mode supplies a missing `;` only when the whole input failed to parse, and a block always parses.
            string body = NeedsTerminator(code) ? code + ";" : code;

            return new Attempt
            {
                Label = label,
                Source = BlockPrefix + body + BlockSuffix,
                RowOffset = 1
            };
        }

        private static bool NeedsTerminator(string code)
        {
            int last = LastIndexOfNonWhitespace(code);
            if (last < 0) return false;

            return code[last] != ';';
        }

        private static Attempt BuildValueReturningAttempt(string code)
        {
            const string Label = "as statements with value";

            int split = FindLastStatementSplit(code);

            string head = split >= 0 ? code.Substring(0, split + 1) : string.Empty;
            var tail = new StringBuilder(code.Substring(split + 1));

            int first = IndexOfNonWhitespace(tail, 0);
            if (first >= 0 && StartsWithReturnKeyword(tail, first))
            {
                for (int i = first; i < first + "return".Length; i++) tail[i] = ' ';
            }

            int last = LastIndexOfNonWhitespace(tail);
            if (last >= 0 && tail[last] == ';') tail[last] = ' ';

            if (IndexOfNonWhitespace(tail, 0) < 0) return Statements(Label, code);

            if (head.Length == 0)
            {
                return new Attempt
                {
                    Label = Label,
                    Source = BlockPrefix + CaptureOpen + "\n" + tail + "\n" + CaptureClose + BlockSuffix,
                    RowOffset = 2
                };
            }

            int row = 1;
            int rowStart = 0;
            for (int i = 0; i <= split; i++)
            {
                if (code[i] == '\n') { row++; rowStart = i + 1; }
            }

            return new Attempt
            {
                Label = Label,
                Source = BlockPrefix + head + CaptureOpen + tail + "\n" + CaptureClose + BlockSuffix,
                RowOffset = 1,
                AdjustRow = row,
                ColumnFrom = (split + 1 - rowStart) + 1 + CaptureOpen.Length,
                ColumnDelta = CaptureOpen.Length
            };
        }

        private static bool OpensWithDeclaration(string code)
        {
            int start = 0;
            while (start < code.Length && char.IsWhiteSpace(code[start])) start++;
            if (start >= code.Length) return false;

            if (code[start] == '[') return true;

            int end = start;
            while (end < code.Length && (char.IsLetterOrDigit(code[end]) || code[end] == '_')) end++;
            if (end == start) return false;

            switch (code.Substring(start, end - start))
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

        private static int IndexOfNonWhitespace(StringBuilder text, int from)
        {
            for (int i = from; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i])) return i;
            }
            return -1;
        }

        private static int LastIndexOfNonWhitespace(StringBuilder text)
        {
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(text[i])) return i;
            }
            return -1;
        }

        private static int LastIndexOfNonWhitespace(string text)
        {
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(text[i])) return i;
            }
            return -1;
        }

        private static string JoinDiagnostics(List<Diagnostic> diagnostics)
        {
            diagnostics.Sort((a, b) =>
            {
                if (a.Row != b.Row) return b.Row.CompareTo(a.Row);
                if (a.Column != b.Column) return b.Column.CompareTo(a.Column);
                return a.Order.CompareTo(b.Order);
            });

            var texts = new List<string>(diagnostics.Count);
            foreach (var diagnostic in diagnostics) texts.Add(diagnostic.Text);
            return string.Join("\n", texts.ToArray());
        }

        private static int FindLastStatementSplit(string code)
        {
            int lastMeaningful = -1;
            for (int i = code.Length - 1; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(code[i]))
                {
                    lastMeaningful = i;
                    break;
                }
            }

            int depth = 0;
            int split = -1;
            int index = 0;

            while (index < code.Length)
            {
                char c = code[index];

                if (c == '"' || c == '\'' ||
                    (c == '@' && index + 1 < code.Length && code[index + 1] == '"'))
                {
                    index = SkipLiteral(code, index);
                    continue;
                }

                if (c == '/' && index + 1 < code.Length && code[index + 1] == '*')
                {
                    index = SkipBlockComment(code, index);
                    continue;
                }

                if (c == '(' || c == '[' || c == '{')
                {
                    depth++;
                }
                else if (c == ')' || c == ']')
                {
                    depth--;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && index != lastMeaningful && ClosesStatement(code, index))
                    {
                        split = index;
                    }
                }
                else if (c == ';' && depth == 0 && index != lastMeaningful)
                {
                    split = index;
                }

                index++;
            }

            return split;
        }

        // A `}` also closes an initializer, where the expression continues into an indexer, a call, or an operator.
        private static bool ClosesStatement(string code, int brace)
        {
            for (int i = brace + 1; i < code.Length; i++)
            {
                char c = code[i];
                if (char.IsWhiteSpace(c)) continue;

                if (".,)]};:?+-*/%&|^<>=!~[(".IndexOf(c) >= 0) return false;

                return !IsWordOperatorAt(code, i, "as") && !IsWordOperatorAt(code, i, "is");
            }

            return false;
        }

        private static bool IsWordOperatorAt(string code, int start, string word)
        {
            if (start + word.Length > code.Length) return false;
            if (string.CompareOrdinal(code, start, word, 0, word.Length) != 0) return false;

            int after = start + word.Length;
            if (after == code.Length) return true;

            char next = code[after];
            return char.IsWhiteSpace(next) || next == '(';
        }

        private static bool StartsWithReturnKeyword(StringBuilder statement, int start)
        {
            const string Keyword = "return";

            if (start + Keyword.Length > statement.Length) return false;

            for (int i = 0; i < Keyword.Length; i++)
            {
                if (statement[start + i] != Keyword[i]) return false;
            }

            if (start + Keyword.Length == statement.Length) return true;

            char next = statement[start + Keyword.Length];
            return !(char.IsLetterOrDigit(next) || next == '_');
        }

        private static string Describe(string label, string messages, string note)
        {
            string text = messages == null ? string.Empty : messages.Trim();

            if (!string.IsNullOrEmpty(note))
            {
                text = text.Length == 0 ? note : text + "\n" + note;
            }

            if (text.Length == 0)
            {
                text = "compilation failed with no diagnostics";
            }

            return "[" + label + "] " + text.Replace("\n", "\n  ");
        }

        private static string DescribeException(Exception ex)
        {
            var message = new StringBuilder();
            message.Append(ex.GetType().Name).Append(": ").Append(ex.Message);

            var inner = ex.InnerException;
            while (inner != null)
            {
                message.Append("\n  Inner: ").Append(inner.GetType().Name)
                       .Append(": ").Append(inner.Message);
                inner = inner.InnerException;
            }

            return message.ToString();
        }

        private static object UserFault(Exception ex)
        {
            var userException = Unwrap(ex);

            return new
            {
                success = false,
                error = DescribeException(userException),
                stackTrace = userException.StackTrace,
                result = (object)null
            };
        }

        private static Exception Unwrap(Exception ex)
        {
            var invocationFailure = ex as TargetInvocationException;
            return invocationFailure != null && invocationFailure.InnerException != null
                ? invocationFailure.InnerException
                : ex;
        }

        // Block comments pass through: the wrapper suffix sits on its own line, out of their reach
        private static string StripComments(string code)
        {
            var stripped = new StringBuilder(code.Length);
            int index = 0;

            while (index < code.Length)
            {
                char c = code[index];

                if (c == '/' && index + 1 < code.Length && code[index + 1] == '/')
                {
                    while (index < code.Length && code[index] != '\n' && code[index] != '\r')
                    {
                        index++;
                    }
                    continue;
                }

                int end;

                if (c == '/' && index + 1 < code.Length && code[index + 1] == '*')
                {
                    end = SkipBlockComment(code, index);
                }
                else if (c == '"' || c == '\'' ||
                         (c == '@' && index + 1 < code.Length && code[index + 1] == '"'))
                {
                    end = SkipLiteral(code, index);
                }
                else
                {
                    stripped.Append(c);
                    index++;
                    continue;
                }

                stripped.Append(code, index, end - index);
                index = end;
            }

            return stripped.ToString().TrimEnd();
        }

        private static int SkipLiteral(string code, int start)
        {
            int index = start;

            if (code[index] == '@')
            {
                index += 2;
                while (index < code.Length)
                {
                    if (code[index] == '"')
                    {
                        if (index + 1 < code.Length && code[index + 1] == '"')
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

            while (index < code.Length)
            {
                char c = code[index];

                if (c == '\\')
                {
                    index += 2;
                    continue;
                }

                if (c == quote) return index + 1;

                if (c == '\n') return index;

                index++;
            }

            return index;
        }

        private static int SkipBlockComment(string code, int start)
        {
            int index = start + 2;

            while (index + 1 < code.Length)
            {
                if (code[index] == '*' && code[index + 1] == '/') return index + 2;
                index++;
            }

            return code.Length;
        }
        
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            bool IEqualityComparer<object>.Equals(object x, object y) { return ReferenceEquals(x, y); }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }

        private static object SerializeResult(object result)
        {
            return SerializeResult(result, 0, new HashSet<object>(ReferenceComparer.Instance));
        }

        private static object SerializeResult(object result, int depth, HashSet<object> visiting)
        {
            if (result == null) return null;

            // Unity's Mono turns runaway recursion into a hard crash, not a catchable exception.
            if (depth >= SerializeDepthLimit) return "... depth limit";

            if (result is GameObject go)
            {
                return new
                {
                    type = "GameObject",
                    name = go.name,
                    instanceId = go.GetInstanceID(),
                    isActive = go.activeSelf
                };
            }
            
            if (result is Component comp)
            {
                return new
                {
                    type = comp.GetType().Name,
                    gameObject = comp.gameObject.name,
                    instanceId = comp.GetInstanceID()
                };
            }
            
            if (result is Vector3 v3)
            {
                return new { x = v3.x, y = v3.y, z = v3.z };
            }
            
            if (result is Vector2 v2)
            {
                return new { x = v2.x, y = v2.y };
            }
            
            if (result is System.Collections.IEnumerable enumerable && !(result is string))
            {
                if (!visiting.Add(result)) return "... circular reference";

                try
                {
                    var list = new List<object>();
                    foreach (var item in enumerable)
                    {
                        list.Add(SerializeResult(item, depth + 1, visiting));
                        if (list.Count > 100)
                        {
                            list.Add("... truncated");
                            break;
                        }
                    }
                    return list;
                }
                finally
                {
                    visiting.Remove(result);
                }
            }
            
            if (result.GetType().IsPrimitive || result is string)
            {
                return result;
            }
            
            string text;
            try
            {
                text = result.ToString();
            }
            catch (Exception ex)
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
    
    public class ConsoleReportPrinter : ReportPrinter
    {
        private readonly StringBuilder output;

        public int RowOffset;

        public int AdjustRow;
        public int ColumnFrom;
        public int ColumnDelta;

        public int MaxRow;

        public int FirstErrorRow { get; private set; }
        public int FirstErrorColumn { get; private set; }

        public ConsoleReportPrinter(StringBuilder output)
        {
            this.output = output;
        }

        public void BeginAttempt(int rowOffset, int adjustRow, int columnFrom, int columnDelta,
            int maxRow)
        {
            RowOffset = rowOffset;
            AdjustRow = adjustRow;
            ColumnFrom = columnFrom;
            ColumnDelta = columnDelta;
            MaxRow = maxRow;
            FirstErrorRow = 0;
            FirstErrorColumn = 0;
            Reset();
            output.Length = 0;
        }

        public override void Print(AbstractMessage msg, bool showFullPath)
        {
            // Without chaining to the base, ErrorsCount never moves and CompileBlock treats an erroring compile as success.
            base.Print(msg, showFullPath);

            var line = new StringBuilder();

            var location = msg.Location;
            if (!location.IsNull)
            {
                int row = location.Row - RowOffset;
                if (row < 1) row = location.Row;

                int column = location.Column;
                if (AdjustRow > 0 && row == AdjustRow && column >= ColumnFrom)
                {
                    int adjusted = column - ColumnDelta;
                    if (adjusted >= 1) column = adjusted;
                }

                if (MaxRow > 0 && row > MaxRow)
                {
                    line.Append("(past end of input): ");
                }
                else
                {
                    if (!msg.IsWarning && FirstErrorRow == 0)
                    {
                        FirstErrorRow = row;
                        FirstErrorColumn = column;
                    }

                    line.Append('(').Append(row).Append(',').Append(column).Append("): ");
                }
            }

            line.Append(msg.MessageType).Append(" CS").Append(msg.Code.ToString("0000"))
                .Append(": ").Append(msg.Text);

            output.AppendLine(line.ToString());
        }
    }
}