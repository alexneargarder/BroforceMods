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

        // Represents void return type
        private sealed class VoidType
        {
            public static readonly VoidType Value = new VoidType();
            private VoidType() { }
        }

        private static void Initialize()
        {
            lock (initLock)
            {
                if (initialized) return;

                try
                {
                    // Force load game assemblies by touching key types BEFORE evaluator creation
                    // This ensures AppDomain.GetAssemblies() returns complete list
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

                    var printer = new ConsoleReportPrinter(output);
                    var context = new CompilerContext(settings, printer);

                    // CRITICAL: Create evaluator AFTER forcing assembly loads
                    evaluator = new Evaluator(context);

                    // Standard libraries to skip
                    var stdLib = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "mscorlib", "System.Core", "System", "System.Xml"
                    };

                    // CRITICAL: Reference assemblies AFTER evaluator creation
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            string name = assembly.GetName().Name;
                            if (stdLib.Contains(name))
                                continue;

                            if (!string.IsNullOrEmpty(assembly.Location))
                            {
                                evaluator.ReferenceAssembly(assembly);
                            }
                        }
                        catch (Exception)
                        {
                            // Some assemblies might fail to load
                        }
                    }

                    // CRITICAL: Run using statements separately, not all at once
                    evaluator.Run("using System;");
                    evaluator.Run("using System.Collections.Generic;");
                    evaluator.Run("using System.Linq;");
                    evaluator.Run("using UnityEngine;");

                    // CRITICAL: Execute meaningful warm-up code that touches referenced types
                    // Simple "1+1" doesn't work - must access actual type system
                    evaluator.Run("var _warmup = new System.Collections.Generic.List<int>();");
                    evaluator.Run("_warmup.Add(1);");
                    evaluator.Run("var _go = typeof(UnityEngine.GameObject);");

                    // Touch game-specific types to ensure their assemblies are fully loaded
                    try
                    {
                        evaluator.Run("var _lsc = typeof(LevelSelectionController);");
                        evaluator.Run("var _hc = typeof(HeroController);");
                        evaluator.Run("var _gs = typeof(GameState);");
                    }
                    catch
                    {
                        // Some types might not be available during initialization, that's ok
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
                // Ensure initialization with lock
                if (!initialized)
                {
                    Initialize();
                }

                // Execute all code on the main thread to prevent Unity API crashes
                object executionResult = null;
                Exception executionException = null;

                MainThreadDispatcher.EnqueueAndWait(() =>
                {
                    try
                    {
                        output.Length = 0;

                        // Strip comments from the code
                        string processedCode = StripComments(code);

                        // Use the same approach as RuntimeUnityEditor - compile and invoke
                        object result = VoidType.Value;
                        CompiledMethod compiled;
                        evaluator.Compile(processedCode, out compiled);

                        if (compiled == null)
                        {
                            // Check for compilation errors
                            string outputStr = output.ToString();
                            if (!string.IsNullOrEmpty(outputStr))
                            {
                                Main.Log($"Compilation failed: {outputStr}");
                                executionResult = new
                                {
                                    success = false,
                                    error = outputStr,
                                    result = (object)null
                                };
                            }
                            else
                            {
                                executionResult = new
                                {
                                    success = false,
                                    error = "Compilation failed with no output",
                                    result = (object)null
                                };
                            }
                            return;
                        }

                        compiled.Invoke(ref result);

                        // Return the result
                        bool hasResult = result != null && !ReferenceEquals(result, VoidType.Value);
                        executionResult = new
                        {
                            success = true,
                            result = hasResult ? SerializeResult(result) : null,
                            resultSet = hasResult,
                            output = output.ToString()
                        };
                    }
                    catch (InternalErrorException iex)
                    {
                        // Don't destroy evaluator on first errors - it may be warming up
                        executionException = iex;
                    }
                    catch (Exception ex)
                    {
                        executionException = ex;
                    }
                }, 5000);

                // Check if there was an exception during execution
                if (executionException != null)
                {
                    // Check if it's an InteractiveHost/TypeLoad error and we have retries left
                    if (retriesRemaining > 0 &&
                        (executionException.Message.Contains("InteractiveHost") ||
                         executionException.Message.Contains("Unexpected error when loading type") ||
                         executionException is TypeLoadException ||
                         executionException is InternalErrorException))
                    {
                        // Silent retry - no logging spam
                        System.Threading.Thread.Sleep(300);
                        return ExecuteWithRetry(code, retriesRemaining: retriesRemaining - 1);
                    }

                    // All retries exhausted
                    Main.Log($"Code execution failed after retries: {executionException.Message}");

                    // If it was an InternalErrorException, force re-init for next call
                    if (executionException is InternalErrorException)
                    {
                        lock (initLock)
                        {
                            initialized = false;
                            evaluator = null;
                        }
                    }

                    return new
                    {
                        success = false,
                        error = $"Execution error: {executionException.Message}",
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
                    error = "Code execution timed out after 5 seconds",
                    result = (object)null
                };
            }
            catch (Exception ex)
            {
                Main.Log($"Code execution error: {ex.Message}");
                return new
                {
                    success = false,
                    error = ex.Message,
                    result = (object)null
                };
            }
        }

        
        private static string StripComments(string code)
        {
            // Strip single-line comments while preserving them inside strings
            var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var result = new List<string>();
            
            foreach (var line in lines)
            {
                string processedLine = line;
                
                // Find comment position (// not inside a string)
                int commentIndex = -1;
                bool inString = false;
                char stringChar = '\0';
                
                for (int i = 0; i < line.Length - 1; i++)
                {
                    // Check for string start/end
                    if (!inString && (line[i] == '"' || line[i] == '\''))
                    {
                        inString = true;
                        stringChar = line[i];
                    }
                    else if (inString && line[i] == stringChar)
                    {
                        // Check if it's escaped
                        if (i > 0 && line[i - 1] == '\\')
                        {
                            // Count consecutive backslashes
                            int backslashCount = 0;
                            for (int j = i - 1; j >= 0 && line[j] == '\\'; j--)
                            {
                                backslashCount++;
                            }
                            // If odd number of backslashes, the quote is escaped
                            if (backslashCount % 2 == 1)
                                continue;
                        }
                        inString = false;
                        stringChar = '\0';
                    }
                    // Check for comment start
                    else if (!inString && i < line.Length - 1 && line[i] == '/' && line[i + 1] == '/')
                    {
                        commentIndex = i;
                        break;
                    }
                }
                
                // Remove comment if found
                if (commentIndex >= 0)
                {
                    processedLine = line.Substring(0, commentIndex).TrimEnd();
                }
                
                // Only add non-empty lines (after trimming whitespace)
                string trimmed = processedLine.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    result.Add(trimmed);
                }
            }
            
            // Join lines and remove empty lines at start/end
            return string.Join("\n", result.ToArray()).Trim();
        }
        
        private static object SerializeResult(object result)
        {
            if (result == null) return null;
            
            // Handle Unity types
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
            
            // Handle collections
            if (result is System.Collections.IEnumerable enumerable && !(result is string))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(SerializeResult(item));
                    if (list.Count > 100) // Limit array size
                    {
                        list.Add("... truncated");
                        break;
                    }
                }
                return list;
            }
            
            // For primitive types and strings, return as-is
            if (result.GetType().IsPrimitive || result is string)
            {
                return result;
            }
            
            // For other objects, return type and ToString()
            return new
            {
                type = result.GetType().FullName,
                value = result.ToString()
            };
        }
    }
    
    // Custom report printer to capture compiler output
    public class ConsoleReportPrinter : ReportPrinter
    {
        private StringBuilder output;
        
        public ConsoleReportPrinter(StringBuilder output)
        {
            this.output = output;
        }
        
        public override void Print(AbstractMessage msg, bool showFullPath)
        {
            var fullMessage = $"{msg.Location}: {msg.Text}";
            output.AppendLine(fullMessage);
            // Don't log compiler messages - they're captured in output for error reporting
        }
    }
}