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
            public string Origin;

            // A teardown stage that timed out never ran, so a later cleanup has to run it again while
            // the stages that did run must not be repeated. The destroy stage records itself: the
            // action's own Clear() leaves TrackedGameObjects empty only when it ran.
            public bool HarmonyUnpatched;

            public volatile bool UnloadRan;
            public volatile bool UnloadRunning;
            public volatile Exception UnloadError;

            public volatile bool MainRunning;
        }

        private class MainOutputCapture
        {
            private readonly List<string> lines = new List<string>();
            private readonly Action<string> inner;
            private bool capturing = true;

            public MainOutputCapture( Action<string> inner )
            {
                this.inner = inner;
            }

            public void Log( string msg )
            {
                if ( capturing )
                {
                    lines.Add( msg );
                }
                inner( msg );
            }

            public string[] Stop()
            {
                capturing = false;
                var result = lines.ToArray();
                lines.Clear();
                return result;
            }
        }

        private class CleanupResult
        {
            public readonly List<string> Errors = new List<string>();
            public string Incomplete;

            public void AddIncomplete( string message )
            {
                Errors.Add( message );
                Incomplete = Incomplete == null ? message : Incomplete + "; " + message;
            }
        }

        private static readonly Dictionary<string, ActiveScript> activeScripts =
            new Dictionary<string, ActiveScript>( StringComparer.OrdinalIgnoreCase );

        public static object ExecuteScript( string name, string source, Dictionary<string, string> args,
            string origin )
        {
            ScriptCompiler.PatchTokenCheck();
            ScriptCompiler.PatchExtensionMethodCrash();
            ScriptCompiler.PatchAssemblyImport();
            ScriptCompiler.PatchConstantImport();
            ScriptCompiler.PatchAssemblyLocation();
            ScriptCompiler.PatchNestedTypeImport();
            var hash = ComputeHash( source );

            ActiveScript existing;
            activeScripts.TryGetValue( name, out existing );

            if ( existing != null && origin != null && existing.Origin != null
                 && !string.Equals( existing.Origin, origin, StringComparison.Ordinal ) )
            {
                return new
                {
                    success = false,
                    error = "Script '" + name + "' is already loaded from " + existing.Origin
                            + ", a different file from " + origin + ". Unload it with unload_script first,"
                            + " or rename one of the files."
                };
            }

            // Harmony's PatchInfo does not deduplicate, so re-entering Main() would stack patches and fire twice.
            if ( existing != null && existing.SourceHash == hash )
            {
                var restart = CleanupScript( name, existing );
                LogCleanupWarnings( name, "restart", restart.Errors );

                if ( restart.Incomplete != null )
                {
                    return StillLoadedRefusal( name, "restarted", restart );
                }

                // Main() runs again, so the next teardown starts from the top.
                existing.UnloadRan = false;
                existing.HarmonyUnpatched = false;
                existing.Origin = origin ?? existing.Origin;
                return InvokeMain( name, existing, args );
            }

            if ( existing != null )
            {
                var cleanup = UnloadInternal( name, existing );
                LogCleanupWarnings( name, "replace", cleanup.Errors );

                if ( cleanup.Incomplete != null )
                {
                    return StillLoadedRefusal( name, "replaced", cleanup );
                }
            }

            var result = ScriptCompiler.Compile( name, source );
            if ( !result.Success )
            {
                return new
                {
                    success = false,
                    error = "Compilation failed",
                    compilerErrors = result.Errors
                };
            }

            Action<string> persistentLogger = msg => Main.Log( "[Script:" + name + "] " + msg );
            var script = new ActiveScript
            {
                Assembly = result.Assembly,
                HarmonyInstance = new Harmony( "script." + name ),
                TrackedGameObjects = new List<GameObject>(),
                SourceHash = hash,
                Logger = persistentLogger,
                Origin = origin
            };

            foreach ( var type in result.Assembly.GetTypes() )
            {
                var unload = type.GetMethod( "Unload", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );
                if ( unload != null && unload.GetParameters().Length == 0 )
                {
                    script.UnloadMethod = unload;
                    break;
                }
            }

            activeScripts[name] = script;

            CodeExecutor.RegisterScriptAssembly( result.Assembly );

            return InvokeMain( name, script, args );
        }

        public static object CompileOnly( string name, string source )
        {
            ScriptCompiler.PatchTokenCheck();
            ScriptCompiler.PatchExtensionMethodCrash();
            ScriptCompiler.PatchAssemblyImport();
            ScriptCompiler.PatchConstantImport();
            ScriptCompiler.PatchAssemblyLocation();
            ScriptCompiler.PatchNestedTypeImport();
            var result = ScriptCompiler.Compile( name, source );
            if ( !result.Success )
            {
                return new
                {
                    success = false,
                    error = "Compilation failed",
                    compilerErrors = result.Errors
                };
            }

            var types = result.Assembly.GetTypes()
                .Select( t => t.FullName )
                .ToArray();

            bool hasMain = result.Assembly.GetTypes()
                .Any( t => t.GetMethod( "Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static ) != null );

            bool hasUnload = result.Assembly.GetTypes()
                .Any( t => t.GetMethod( "Unload", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static ) != null );

            return new
            {
                success = true,
                types,
                hasMain,
                hasUnload
            };
        }

        private static CleanupResult CleanupScript( string name, ActiveScript script )
        {
            var result = new CleanupResult();

            if ( script.MainRunning )
            {
                result.AddIncomplete( "Main() is still running" );
                return result;
            }

            bool unloadRunning = script.UnloadRunning;
            if ( script.UnloadMethod != null && !script.UnloadRan )
            {
                if ( unloadRunning )
                {
                    result.AddIncomplete( "Unload() is still running from an earlier teardown" );
                    return result;
                }

                try
                {
                    MainThreadDispatcher.EnqueueAndWait( () => RunUnload( script ), 10000 );
                }
                catch ( TimeoutException ex )
                {
                    result.AddIncomplete( "Unload() timed out: " + ex.Message );
                    return result;
                }
                catch ( MainThreadDispatcher.MainThreadActionRunningException ex )
                {
                    result.AddIncomplete( "Unload() is still running: " + ex.Message );
                    return result;
                }
            }

            var unloadError = script.UnloadError;
            if ( unloadError != null )
            {
                script.UnloadError = null;
                result.Errors.Add( "Unload() failed: " + DescribeException( unloadError ) );
            }

            if ( !script.HarmonyUnpatched )
            {
                try
                {
                    script.HarmonyInstance.UnpatchAll( "script." + name );
                }
                catch ( Exception ex )
                {
                    result.Errors.Add( "Harmony unpatch failed: " + ex.Message );
                }

                script.HarmonyUnpatched = true;
            }

            if ( script.TrackedGameObjects.Count > 0 )
            {
                try
                {
                    MainThreadDispatcher.EnqueueAndWait( () =>
                    {
                        foreach ( var go in script.TrackedGameObjects )
                        {
                            if ( go != null )
                            {
                                UnityEngine.Object.Destroy( go );
                            }
                        }

                        // Cleared inside the action: a wait that timed out destroyed nothing, and a clear
                        // outside it would drop the only handles on objects that are still alive.
                        script.TrackedGameObjects.Clear();
                    }, 5000 );
                }
                catch ( TimeoutException ex )
                {
                    result.AddIncomplete( "GameObject cleanup timed out: " + ex.Message );
                    return result;
                }
                catch ( MainThreadDispatcher.MainThreadActionRunningException ex )
                {
                    result.AddIncomplete( "GameObject cleanup is still running: " + ex.Message );
                    return result;
                }
                catch ( Exception ex )
                {
                    result.Errors.Add( "GameObject cleanup failed: " + ex.Message );
                }
            }

            return result;
        }

        private static void RunUnload( ActiveScript script )
        {
            script.UnloadRunning = true;
            var saved = ScriptContext.Enter( script.HarmonyInstance, script.Logger,
                script.TrackedGameObjects, script.Args );
            try
            {
                script.UnloadMethod.Invoke( null, null );
            }
            catch ( TargetInvocationException tie )
            {
                script.UnloadError = tie.InnerException ?? tie;
            }
            catch ( Exception ex )
            {
                script.UnloadError = ex;
            }
            finally
            {
                ScriptContext.Exit( saved );
                script.UnloadRan = true;
                script.UnloadRunning = false;
            }
        }

        private static string DescribeException( Exception ex )
        {
            var text = ex.GetType().FullName + ": " + ex.Message;
            return string.IsNullOrEmpty( ex.StackTrace ) ? text : text + "\n" + ex.StackTrace;
        }

        private static object StillLoadedRefusal( string name, string action, CleanupResult cleanup )
        {
            return new
            {
                success = false,
                error = "Script '" + name + "' is still loaded and was not " + action + ": "
                        + cleanup.Incomplete
                        + ". The teardown stopped at that stage and the ones after it have not run; the ones"
                        + " before it stay done, so unload_script can be called to finish it before the"
                        + " script is loaded again.",
                warnings = cleanup.Errors.ToArray()
            };
        }

        private static void LogCleanupWarnings( string name, string action, List<string> warnings )
        {
            foreach ( var warning in warnings )
            {
                Main.Log( "[Script:" + name + "] " + action + ": " + warning );
            }
        }

        private static CleanupResult UnloadInternal( string name, ActiveScript script )
        {
            var result = CleanupScript( name, script );

            if ( result.Incomplete != null )
            {
                return result;
            }

            activeScripts.Remove( name );
            CodeExecutor.UnregisterScriptAssembly( script.Assembly );

            return result;
        }

        public static object UnloadScript( string name )
        {
            ActiveScript script;
            if ( !activeScripts.TryGetValue( name, out script ) )
            {
                return new { success = false, error = "Script not found: " + name };
            }

            var result = UnloadInternal( name, script );

            if ( result.Incomplete != null )
            {
                return new
                {
                    success = false,
                    error = "Script '" + name + "' is still loaded: " + result.Incomplete
                            + ". The teardown stopped at that stage and the ones after it have not run; the"
                            + " ones before it stay done, so unload_script can be called again to finish it.",
                    warnings = result.Errors.ToArray()
                };
            }

            if ( result.Errors.Count > 0 )
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
            var scripts = activeScripts.Select( kvp => new
            {
                name = kvp.Key,
                origin = kvp.Value.Origin,
                hasUnload = kvp.Value.UnloadMethod != null,
                trackedObjects = kvp.Value.TrackedGameObjects.Count,
                types = kvp.Value.Assembly.GetTypes().Select( t => t.FullName ).ToArray()
            } ).ToArray();

            return new { count = scripts.Length, scripts };
        }

        internal static List<Assembly> LoadedScriptAssemblies( string exceptName )
        {
            return activeScripts
                .Where( kvp => !string.Equals( kvp.Key, exceptName, StringComparison.OrdinalIgnoreCase ) )
                .Select( kvp => kvp.Value.Assembly )
                .ToList();
        }

        private static object InvokeMain( string name, ActiveScript script, Dictionary<string, string> args )
        {
            script.Args = args ?? new Dictionary<string, string>();

            MethodInfo mainMethod = null;
            foreach ( var type in script.Assembly.GetTypes() )
            {
                var main = type.GetMethod( "Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );
                if ( main != null && main.GetParameters().Length == 0 )
                {
                    mainMethod = main;
                    break;
                }
            }

            if ( mainMethod == null )
            {
                var types = script.Assembly.GetTypes()
                    .Select( t => t.FullName )
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
            var capture = new MainOutputCapture( script.Logger );
            string[] output = null;

            try
            {
                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    script.MainRunning = true;
                    var saved = ScriptContext.Enter( script.HarmonyInstance, capture.Log,
                        script.TrackedGameObjects, script.Args );
                    try
                    {
                        mainMethod.Invoke( null, null );
                    }
                    catch ( TargetInvocationException tie )
                    {
                        executionException = tie.InnerException ?? tie;
                    }
                    catch ( Exception ex )
                    {
                        executionException = ex;
                    }
                    finally
                    {
                        ScriptContext.Exit( saved );
                        output = capture.Stop();
                        script.MainRunning = false;
                    }
                }, 30000 );
            }
            catch ( TimeoutException ex )
            {
                activeScripts.Remove( name );
                CodeExecutor.UnregisterScriptAssembly( script.Assembly );
                return new
                {
                    success = false,
                    error = "Main() did not start and the script was not loaded: " + ex.Message
                                                                                   + ". The game's main thread was busy; execute_script can be called again."
                };
            }
            catch ( MainThreadDispatcher.MainThreadActionRunningException ex )
            {
                return new
                {
                    success = false,
                    error = "Script '" + name + "' is loaded but Main() is still running: " + ex.Message
                            + ". Its output and outcome are not reported; unload_script tears it down once"
                            + " Main() has returned."
                };
            }

            if ( output == null )
            {
                output = new string[0];
            }

            if ( executionException != null )
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

        private static string ComputeHash( string source )
        {
            using ( var md5 = MD5.Create() )
            {
                var bytes = Encoding.UTF8.GetBytes( source );
                var hash = md5.ComputeHash( bytes );
                var sb = new StringBuilder();
                foreach ( var b in hash )
                {
                    sb.Append( b.ToString( "x2" ) );
                }
                return sb.ToString();
            }
        }
    }
}
