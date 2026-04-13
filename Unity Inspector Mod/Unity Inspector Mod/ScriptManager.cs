using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public static class ScriptManager
    {
        private class ActiveScript
        {
            public Assembly Assembly;
            public MethodInfo UnloadMethod;
            public Harmony HarmonyInstance;
            public List<GameObject> TrackedGameObjects;
            public string SourceHash;
            public Action<string> Logger;
        }

        private static readonly Dictionary<string, ActiveScript> activeScripts =
            new Dictionary<string, ActiveScript>(StringComparer.OrdinalIgnoreCase);

        public static object ExecuteScript(string name, string source, Dictionary<string, string> args)
        {
            ScriptCompiler.PatchTokenCheck();
            ScriptCompiler.PatchExtensionMethodCrash();
            var hash = ComputeHash(source);

            // If same script is already loaded with same source, just re-run Main()
            ActiveScript existing;
            if (activeScripts.TryGetValue(name, out existing) && existing.SourceHash == hash)
            {
                return InvokeMain(name, existing, args);
            }

            // If same script exists with different source, unload first
            if (existing != null)
            {
                UnloadScript(name);
            }

            var result = ScriptCompiler.Compile(name, source);
            if (!result.Success)
            {
                return new
                {
                    success = false,
                    error = "Compilation failed",
                    compilerErrors = result.Errors
                };
            }

            Action<string> persistentLogger = msg => Main.Log("[Script:" + name + "] " + msg);
            var script = new ActiveScript
            {
                Assembly = result.Assembly,
                HarmonyInstance = new Harmony("script." + name),
                TrackedGameObjects = new List<GameObject>(),
                SourceHash = hash,
                Logger = persistentLogger
            };

            // Find Unload method
            foreach (var type in result.Assembly.GetTypes())
            {
                var unload = type.GetMethod("Unload", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (unload != null && unload.GetParameters().Length == 0)
                {
                    script.UnloadMethod = unload;
                    break;
                }
            }

            activeScripts[name] = script;

            // Register assembly with the Evaluator so execute_code can see compiled types
            try
            {
                var evaluatorField = typeof(CodeExecutor).GetField("evaluator",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (evaluatorField != null)
                {
                    var evaluator = evaluatorField.GetValue(null) as Mono.CSharp.Evaluator;
                    if (evaluator != null)
                    {
                        evaluator.ReferenceAssembly(result.Assembly);
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Log("Failed to register script assembly with Evaluator: " + ex.Message);
            }

            return InvokeMain(name, script, args);
        }

        public static object CompileOnly(string name, string source)
        {
            ScriptCompiler.PatchTokenCheck();
            ScriptCompiler.PatchExtensionMethodCrash();
            var result = ScriptCompiler.Compile(name, source);
            if (!result.Success)
            {
                return new
                {
                    success = false,
                    error = "Compilation failed",
                    compilerErrors = result.Errors
                };
            }

            var types = result.Assembly.GetTypes()
                .Select(t => t.FullName)
                .ToArray();

            bool hasMain = result.Assembly.GetTypes()
                .Any(t => t.GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) != null);

            bool hasUnload = result.Assembly.GetTypes()
                .Any(t => t.GetMethod("Unload", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) != null);

            return new
            {
                success = true,
                types,
                hasMain,
                hasUnload
            };
        }

        public static object UnloadScript(string name)
        {
            ActiveScript script;
            if (!activeScripts.TryGetValue(name, out script))
            {
                return new { success = false, error = "Script not found: " + name };
            }

            var errors = new List<string>();

            // Invoke Unload() on main thread
            if (script.UnloadMethod != null)
            {
                try
                {
                    MainThreadDispatcher.EnqueueAndWait(() =>
                    {
                        // Set up context for Unload
                        ScriptContext.Harmony = script.HarmonyInstance;
                        ScriptContext.Logger = msg => Main.Log("[Script:" + name + "] " + msg);
                        ScriptContext.GameObjects = script.TrackedGameObjects;

                        try
                        {
                            script.UnloadMethod.Invoke(null, null);
                        }
                        finally
                        {
                            ScriptContext.Clear();
                        }
                    }, 10000);
                }
                catch (Exception ex)
                {
                    errors.Add("Unload() failed: " + ex.Message);
                }
            }

            // Unpatch all Harmony patches from this script
            try
            {
                script.HarmonyInstance.UnpatchAll("script." + name);
            }
            catch (Exception ex)
            {
                errors.Add("Harmony unpatch failed: " + ex.Message);
            }

            // Destroy tracked GameObjects on main thread
            if (script.TrackedGameObjects.Count > 0)
            {
                try
                {
                    MainThreadDispatcher.EnqueueAndWait(() =>
                    {
                        foreach (var go in script.TrackedGameObjects)
                        {
                            if (go != null)
                            {
                                UnityEngine.Object.Destroy(go);
                            }
                        }
                    }, 5000);
                }
                catch (Exception ex)
                {
                    errors.Add("GameObject cleanup failed: " + ex.Message);
                }
            }

            activeScripts.Remove(name);

            if (errors.Count > 0)
            {
                return new
                {
                    success = true,
                    warnings = errors.ToArray()
                };
            }

            return new { success = true };
        }

        public static object ListActiveScripts()
        {
            var scripts = activeScripts.Select(kvp => new
            {
                name = kvp.Key,
                hasUnload = kvp.Value.UnloadMethod != null,
                trackedObjects = kvp.Value.TrackedGameObjects.Count,
                types = kvp.Value.Assembly.GetTypes().Select(t => t.FullName).ToArray()
            }).ToArray();

            return new { count = scripts.Length, scripts };
        }

        private static object InvokeMain(string name, ActiveScript script, Dictionary<string, string> args)
        {
            // Find Main() method
            MethodInfo mainMethod = null;
            foreach (var type in script.Assembly.GetTypes())
            {
                var main = type.GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (main != null && main.GetParameters().Length == 0)
                {
                    mainMethod = main;
                    break;
                }
            }

            if (mainMethod == null)
            {
                // No Main() is valid -- script only defines types for REPL
                var types = script.Assembly.GetTypes()
                    .Select(t => t.FullName)
                    .ToArray();

                return new
                {
                    success = true,
                    executed = false,
                    message = "Script compiled and registered (no Main() found)",
                    types
                };
            }

            // Set up ScriptContext and invoke on main thread, capturing log output
            object executionResult = null;
            Exception executionException = null;
            var logOutput = new List<string>();

            MainThreadDispatcher.EnqueueAndWait(() =>
            {
                ScriptContext.Harmony = script.HarmonyInstance;
                ScriptContext.Logger = msg =>
                {
                    logOutput.Add(msg);
                    script.Logger(msg);
                };
                ScriptContext.GameObjects = script.TrackedGameObjects;
                ScriptContext.Args = args ?? new Dictionary<string, string>();

                try
                {
                    mainMethod.Invoke(null, null);
                    executionResult = new
                    {
                        success = true,
                        executed = true,
                        scriptName = name,
                        output = logOutput.ToArray()
                    };
                }
                catch (TargetInvocationException tie)
                {
                    executionException = tie.InnerException ?? tie;
                }
                catch (Exception ex)
                {
                    executionException = ex;
                }
                finally
                {
                    // Set Logger to the persistent version (not the capturing one)
                    // so Harmony patches that fire after Main() can still log
                    ScriptContext.Logger = script.Logger;
                    ScriptContext.Args = null;
                }
            }, 30000);

            if (executionException != null)
            {
                return new
                {
                    success = false,
                    error = "Main() threw an exception: " + executionException.Message,
                    stackTrace = executionException.StackTrace,
                    output = logOutput.ToArray()
                };
            }

            return executionResult;
        }

        private static string ComputeHash(string source)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(source);
                var hash = md5.ComputeHash(bytes);
                var sb = new StringBuilder();
                foreach (var b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
