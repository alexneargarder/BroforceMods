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
            public Dictionary<string, string> Args;
        }

        private class MainOutputCapture
        {
            private readonly List<string> lines = new List<string>();
            private readonly Action<string> inner;
            private bool capturing = true;

            public MainOutputCapture(Action<string> inner)
            {
                this.inner = inner;
            }

            public void Log(string msg)
            {
                if (capturing) lines.Add(msg);
                inner(msg);
            }

            public string[] Stop()
            {
                capturing = false;
                var result = lines.ToArray();
                lines.Clear();
                return result;
            }
        }

        private static readonly Dictionary<string, ActiveScript> activeScripts =
            new Dictionary<string, ActiveScript>(StringComparer.OrdinalIgnoreCase);

        public static object ExecuteScript(string name, string source, Dictionary<string, string> args)
        {
            ScriptCompiler.PatchTokenCheck();
            ScriptCompiler.PatchExtensionMethodCrash();
            ScriptCompiler.PatchAssemblyImport();
            var hash = ComputeHash(source);

            // Restart rather than re-enter Main(): Harmony's PatchInfo does not deduplicate,
            // so patches would stack and fire twice.
            ActiveScript existing;
            if (activeScripts.TryGetValue(name, out existing) && existing.SourceHash == hash)
            {
                LogCleanupWarnings(name, "restart", CleanupScript(name, existing));
                return InvokeMain(name, existing, args);
            }

            // If same script exists with different source, unload first
            if (existing != null)
            {
                LogCleanupWarnings(name, "replace", UnloadInternal(name, existing));
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
            CodeExecutor.RegisterScriptAssembly(result.Assembly);

            return InvokeMain(name, script, args);
        }

        public static object CompileOnly(string name, string source)
        {
            ScriptCompiler.PatchTokenCheck();
            ScriptCompiler.PatchExtensionMethodCrash();
            ScriptCompiler.PatchAssemblyImport();
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

        private static List<string> CleanupScript(string name, ActiveScript script)
        {
            var errors = new List<string>();

            // Invoke Unload() on main thread
            if (script.UnloadMethod != null)
            {
                try
                {
                    MainThreadDispatcher.EnqueueAndWait(() =>
                    {
                        var saved = ScriptContext.Enter(script.HarmonyInstance, script.Logger,
                            script.TrackedGameObjects, script.Args);
                        try
                        {
                            script.UnloadMethod.Invoke(null, null);
                        }
                        finally
                        {
                            ScriptContext.Exit(saved);
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

                        // Cleared inside the action: EnqueueAndWait's timeout only stops
                        // waiting, the action stays queued.
                        script.TrackedGameObjects.Clear();
                    }, 5000);
                }
                catch (Exception ex)
                {
                    errors.Add("GameObject cleanup failed: " + ex.Message);
                }
            }

            return errors;
        }

        private static void LogCleanupWarnings(string name, string action, List<string> warnings)
        {
            foreach (var warning in warnings)
                Main.Log("[Script:" + name + "] " + action + ": " + warning);
        }

        private static List<string> UnloadInternal(string name, ActiveScript script)
        {
            var errors = CleanupScript(name, script);

            activeScripts.Remove(name);
            CodeExecutor.UnregisterScriptAssembly(script.Assembly);

            return errors;
        }

        public static object UnloadScript(string name)
        {
            ActiveScript script;
            if (!activeScripts.TryGetValue(name, out script))
            {
                return new { success = false, error = "Script not found: " + name };
            }

            var errors = UnloadInternal(name, script);

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
            script.Args = args ?? new Dictionary<string, string>();

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
            Exception executionException = null;
            var capture = new MainOutputCapture(script.Logger);
            string[] output = null;

            MainThreadDispatcher.EnqueueAndWait(() =>
            {
                var saved = ScriptContext.Enter(script.HarmonyInstance, capture.Log,
                    script.TrackedGameObjects, script.Args);
                try
                {
                    mainMethod.Invoke(null, null);
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
                    ScriptContext.Exit(saved);
                    output = capture.Stop();
                }
            }, 30000);

            if (output == null) output = new string[0];

            if (executionException != null)
            {
                return new
                {
                    success = false,
                    error = "Main() threw an exception: " + executionException.Message,
                    stackTrace = executionException.StackTrace,
                    output
                };
            }

            return new
            {
                success = true,
                executed = true,
                scriptName = name,
                output
            };
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
