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
        // Represents void return type
        private sealed class VoidType
        {
            public static readonly VoidType Value = new VoidType();
            private VoidType() { }
        }
        
        static CodeExecutor()
        {
            Initialize();
        }
        
        private static void Initialize()
        {
            try
            {
                output = new StringBuilder();
                
                // Use exact same setup as RuntimeUnityEditor
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
                
                evaluator = new Evaluator(context);
                
                // Standard libraries to skip (they're included via StdLib = true)
                var stdLib = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                {
                    "mscorlib", "System.Core", "System", "System.Xml"
                };
                
                // Import all non-standard assemblies from the AppDomain
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        string name = assembly.GetName().Name;
                        if (stdLib.Contains(name))
                            continue;
                            
                        // Skip assemblies without a location (usually dynamic)
                        if (!string.IsNullOrEmpty(assembly.Location))
                        {
                            evaluator.ReferenceAssembly(assembly);
                        }
                    }
                    catch (Exception)
                    {
                        // Some assemblies might fail to load, that's ok
                    }
                }
                
                // Set up environment exactly like RuntimeUnityEditor does
                var envSetup = "using System;" +
                               "using UnityEngine;" +
                               "using System.Linq;" +
                               "using System.Collections;" +
                               "using System.Collections.Generic;";
                
                // Compile and invoke the using statements
                object result = VoidType.Value;
                CompiledMethod compiled;
                evaluator.Compile(envSetup, out compiled);
                if (compiled != null)
                {
                    compiled.Invoke(ref result);
                }
            }
            catch (Exception ex)
            {
                Main.Log($"Failed to initialize CodeExecutor: {ex.Message}");
                throw;
            }
        }
        
        public static object Execute(string code)
        {
            try
            {
                // Make sure evaluator is initialized
                if (evaluator == null)
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
                        output.Length = 0; // Clear the StringBuilder
                        
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
                    catch (Exception ex)
                    {
                        Main.Log($"Execution error: {ex.Message}");
                        executionException = ex;
                    }
                }, 5000);
                
                // Check if there was an exception during execution
                if (executionException != null)
                {
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
            Main.Log($"Compiler message: {fullMessage}");
        }
    }
}