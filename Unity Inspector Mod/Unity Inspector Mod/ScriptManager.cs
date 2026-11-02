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

            // A teardown stage that timed out never ran, so a later cleanup has to run it again while
            // the stages that did run must not be repeated. The destroy stage records itself: the
            // action's own Clear() leaves TrackedGameObjects empty only when it ran.
            public bool UnloadRan;
            public bool HarmonyUnpatched;
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

        // A teardown wait that timed out did not run, so the script has to stay registered: its
        // registration holds the only handles on the objects a later unload_script must still destroy.
        private class CleanupResult
        {
            public readonly List<string> Errors = new List<string>();
            public string TimedOut;

            public void AddTimeout(string message)
            {
                Errors.Add(message);
                TimedOut = TimedOut == null ? message : TimedOut + "; " + message;
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

            // Harmony's PatchInfo does not deduplicate, so re-entering Main() would stack patches and fire twice.
            ActiveScript existing;
            if (activeScripts.TryGetValue(name, out existing) && existing.SourceHash == hash)
            {
                var restart = CleanupScript(name, existing);
                LogCleanupWarnings(name, "restart", restart.Errors);

                if (restart.TimedOut != null)
                {
                    return StillLoadedRefusal(name, "restarted", restart);
                }

                // Main() runs again, so the next teardown starts from the top.
                existing.UnloadRan = false;
                existing.HarmonyUnpatched = false;
                return InvokeMain(name, existing, args);
            }

            if (existing != null)
            {
                var cleanup = UnloadInternal(name, existing);
                LogCleanupWarnings(name, "replace", cleanup.Errors);

                if (cleanup.TimedOut != null)
                {
                    return StillLoadedRefusal(name, "replaced", cleanup);
                }
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

        // The stages run in order and each is skipped once it is done, so a retry picks up where the
        // timeout left off. A timed-out stage returns at once: the ones after it depend on it having
        // run, and stripping the patches of a script whose Unload() was abandoned is not a teardown.
        // A stage that threw anything else did run, and is not retried.
        private static CleanupResult CleanupScript(string name, ActiveScript script)
        {
            var result = new CleanupResult();

            if (script.UnloadMethod != null && !script.UnloadRan)
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
                catch (TimeoutException ex)
                {
                    result.AddTimeout("Unload() timed out: " + ex.Message);
                    return result;
                }
                catch (Exception ex)
                {
                    result.Errors.Add("Unload() failed: " + ex.Message);
                }

                script.UnloadRan = true;
            }

            if (!script.HarmonyUnpatched)
            {
                try
                {
                    script.HarmonyInstance.UnpatchAll("script." + name);
                }
                catch (Exception ex)
                {
                    result.Errors.Add("Harmony unpatch failed: " + ex.Message);
                }

                script.HarmonyUnpatched = true;
            }

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

                        // Cleared inside the action: a wait that timed out destroyed nothing, and a clear
                        // outside it would drop the only handles on objects that are still alive.
                        script.TrackedGameObjects.Clear();
                    }, 5000);
                }
                catch (TimeoutException ex)
                {
                    result.AddTimeout("GameObject cleanup timed out: " + ex.Message);
                    return result;
                }
                catch (Exception ex)
                {
                    result.Errors.Add("GameObject cleanup failed: " + ex.Message);
                }
            }

            return result;
        }

        // Main() must not run again over an instance whose teardown never ran, so both the restart and
        // the replace path answer with this instead of loading the source.
        private static object StillLoadedRefusal(string name, string action, CleanupResult cleanup)
        {
            return new
            {
                success = false,
                error = "Script '" + name + "' is still loaded and was not " + action + ": "
                    + cleanup.TimedOut
                    + ". That teardown and the ones after it did not run; the ones before it did and"
                    + " stay done, so unload_script can be called to retry the rest before the script"
                    + " is loaded again.",
                warnings = cleanup.Errors.ToArray()
            };
        }

        private static void LogCleanupWarnings(string name, string action, List<string> warnings)
        {
            foreach (var warning in warnings)
                Main.Log("[Script:" + name + "] " + action + ": " + warning);
        }

        private static CleanupResult UnloadInternal(string name, ActiveScript script)
        {
            var result = CleanupScript(name, script);

            if (result.TimedOut != null)
            {
                return result;
            }

            activeScripts.Remove(name);
            CodeExecutor.UnregisterScriptAssembly(script.Assembly);

            return result;
        }

        public static object UnloadScript(string name)
        {
            ActiveScript script;
            if (!activeScripts.TryGetValue(name, out script))
            {
                return new { success = false, error = "Script not found: " + name };
            }

            var result = UnloadInternal(name, script);

            if (result.TimedOut != null)
            {
                return new
                {
                    success = false,
                    error = "Script '" + name + "' is still loaded: " + result.TimedOut
                        + ". That teardown and the ones after it did not run; the ones before it did"
                        + " and stay done, so unload_script can be called again to retry the rest.",
                    warnings = result.Errors.ToArray()
                };
            }

            if (result.Errors.Count > 0)
            {
                return new
                {
                    success = true,
                    warnings = result.Errors.ToArray()
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
