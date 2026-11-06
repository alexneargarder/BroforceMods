using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json.Linq;
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

        private class Teardown
        {
            public readonly List<string> Warnings = new List<string>();
            public int DestroyedObjects;
            public int DestroyedComponents;
        }

        private const int StartBudgetMs = 10000;
        private const int UserCodeBudgetMs = 15000;
        private const int CeilingMs = 45000;

        private static readonly Dictionary<string, ActiveScript> activeScripts =
            new Dictionary<string, ActiveScript>( StringComparer.OrdinalIgnoreCase );

        private static volatile string phase;

        private static object OnMainThread( string what, Func<object> operation )
        {
            object reply = null;
            try
            {
                MainThreadDispatcher.EnqueueAndWaitStoppable( () =>
                {
                    try
                    {
                        reply = operation();
                    }
                    finally
                    {
                        phase = null;
                    }
                }, StartBudgetMs, UserCodeBudgetMs, CeilingMs, what );
                return reply;
            }
            catch ( TimeoutException ex )
            {
                throw new TimeoutException( what + " did not start: " + ex.Message + ". The game's main thread was busy, so " +
                                            "nothing was compiled, run or unloaded, and the call can be made again." );
            }
            catch ( MainThreadDispatcher.MainThreadActionRunningException ex )
            {
                var running = phase;
                throw new MainThreadDispatcher.MainThreadActionRunningException(
                    what + " is " + ex.Message + ( running != null ? " (in " + running + ")" : "" ) +
                    ". The rest of it runs once that returns, and its outcome is not reported." );
            }
        }

        public static object ExecuteScript( string name, string source, Dictionary<string, string> args,
            string origin )
        {
            return OnMainThread( "execute_script " + name, () => ExecuteOnMainThread( name, source, args, origin ) );
        }

        public static object CompileOnly( string name, string source )
        {
            return OnMainThread( "compile_script " + name, () =>
            {
                phase = "the compile";
                var result = ScriptCompiler.Compile( name, source );
                if ( !result.Success )
                {
                    return CompileFailure( result );
                }

                var types = result.Assembly.GetTypes();
                return new
                {
                    success = true,
                    types = types.Select( t => t.FullName ).ToArray(),
                    hasMain = FindEntryPoint( types, "Main" ) != null,
                    hasUnload = FindEntryPoint( types, "Unload" ) != null
                };
            } );
        }

        public static object UnloadScript( string name )
        {
            return OnMainThread( "unload_script " + name, () =>
            {
                ActiveScript script;
                if ( !activeScripts.TryGetValue( name, out script ) )
                {
                    return new
                    {
                        success = false,
                        error = "Script not found: " + name + ". " + LoadedNames()
                    };
                }

                var teardown = TearDown( name, script );
                Unregister( name, script );

                return WithWarnings( new
                {
                    success = true,
                    scriptName = name,
                    destroyedObjects = teardown.DestroyedObjects,
                    destroyedComponents = teardown.DestroyedComponents
                }, teardown.Warnings );
            } );
        }

        public static object ListActiveScripts()
        {
            return OnMainThread( "list_active_scripts", () =>
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
            } );
        }

        internal static List<Assembly> LoadedScriptAssemblies( string exceptName )
        {
            return activeScripts
                .Where( kvp => !string.Equals( kvp.Key, exceptName, StringComparison.OrdinalIgnoreCase ) )
                .Select( kvp => kvp.Value.Assembly )
                .ToList();
        }

        private static string LoadedNames()
        {
            return activeScripts.Count == 0
                ? "No script is loaded."
                : "Loaded scripts: " + string.Join( ", ", activeScripts.Keys.ToArray() ) + ".";
        }

        private static object ExecuteOnMainThread( string name, string source, Dictionary<string, string> args,
            string origin )
        {
            ScriptCompiler.InstallPatches();
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

            var warnings = new List<string>();

            // Harmony's PatchInfo does not deduplicate, so re-entering Main() would stack patches and fire twice.
            if ( existing != null && existing.SourceHash == hash )
            {
                warnings.AddRange( TearDown( name, existing ).Warnings );
                existing.Origin = origin ?? existing.Origin;
                return InvokeMain( name, existing, args, warnings );
            }

            if ( existing != null )
            {
                warnings.AddRange( TearDown( name, existing ).Warnings );
                Unregister( name, existing );
            }

            phase = "the compile";
            var result = ScriptCompiler.Compile( name, source );
            if ( !result.Success )
            {
                return CompileFailure( result );
            }

            var types = result.Assembly.GetTypes();
            var collision = TypeCollision( types );
            if ( collision != null )
            {
                return new { success = false, error = collision };
            }

            Action<string> persistentLogger = msg => Main.Log( "[Script:" + name + "] " + msg );
            var script = new ActiveScript
            {
                Assembly = result.Assembly,
                HarmonyInstance = new Harmony( "script." + name ),
                TrackedGameObjects = new List<GameObject>(),
                SourceHash = hash,
                Logger = persistentLogger,
                Origin = origin,
                UnloadMethod = FindEntryPoint( types, "Unload" )
            };

            activeScripts[name] = script;
            CodeExecutor.RegisterScriptAssembly( result.Assembly );

            return InvokeMain( name, script, args, warnings );
        }

        private static object CompileFailure( ScriptCompiler.CompileResult result )
        {
            return new
            {
                success = false,
                error = "Compilation failed",
                compilerErrors = result.Errors
            };
        }

        // mcs reports a type imported from two loaded assemblies as CS0433 wherever it is named.
        private static string TypeCollision( IEnumerable<Type> types )
        {
            var declared = new HashSet<string>( types
                .Where( t => !t.IsNested && t.FullName != null && t.FullName.IndexOf( '<' ) < 0 )
                .Select( t => t.FullName ) );

            foreach ( var other in activeScripts )
            {
                foreach ( var type in other.Value.Assembly.GetTypes() )
                {
                    if ( !type.IsNested && declared.Contains( type.FullName ) )
                    {
                        return "Type '" + type.FullName + "' is already declared by loaded script '" + other.Key +
                               "'. Unload that script with unload_script or rename the type; with both loaded, " +
                               "neither execute_code nor another script could name it.";
                    }
                }
            }

            return null;
        }

        private static MethodInfo FindEntryPoint( IEnumerable<Type> types, string name )
        {
            foreach ( var type in types )
            {
                var method = type.GetMethods( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly )
                    .FirstOrDefault( m => m.Name == name && m.GetParameters().Length == 0 &&
                                          !m.IsGenericMethodDefinition );
                if ( method != null )
                {
                    return method;
                }
            }

            return null;
        }

        private static void Unregister( string name, ActiveScript script )
        {
            activeScripts.Remove( name );
            CodeExecutor.UnregisterScriptAssembly( script.Assembly );
        }

        private static Teardown TearDown( string name, ActiveScript script )
        {
            var teardown = new Teardown();

            if ( script.UnloadMethod != null )
            {
                phase = "Unload()";
                Exception unloadError = null;
                var saved = ScriptContext.Enter( script.HarmonyInstance, script.Logger,
                    script.TrackedGameObjects, script.Args );
                bool finished;
                try
                {
                    finished = MainThreadDispatcher.RunStoppable( () =>
                    {
                        try
                        {
                            script.UnloadMethod.Invoke( null, null );
                        }
                        catch ( TargetInvocationException tie )
                        {
                            unloadError = tie.InnerException ?? tie;
                        }
                    } );
                }
                catch ( Exception ex )
                {
                    finished = true;
                    unloadError = ex;
                }
                finally
                {
                    ScriptContext.Exit( saved );
                }

                if ( !finished )
                {
                    teardown.Warnings.Add( "Unload() was stopped: it was still running after " +
                                           UserCodeBudgetMs / 1000 + "s" );
                }
                else if ( unloadError != null )
                {
                    teardown.Warnings.Add( "Unload() failed: " + DescribeException( unloadError ) );
                }
            }

            phase = "the teardown";
            foreach ( var original in script.HarmonyInstance.GetPatchedMethods().ToList() )
            {
                try
                {
                    script.HarmonyInstance.Unpatch( original, HarmonyPatchType.All, script.HarmonyInstance.Id );
                }
                catch ( Exception ex )
                {
                    teardown.Warnings.Add( "Harmony unpatch of " + original.DeclaringType?.FullName + "." + original.Name +
                                           " failed: " + ex.Message );
                }
            }

            foreach ( var go in script.TrackedGameObjects )
            {
                if ( go == null )
                {
                    continue;
                }
                try
                {
                    UnityEngine.Object.DestroyImmediate( go );
                    teardown.DestroyedObjects++;
                }
                catch ( Exception ex )
                {
                    teardown.Warnings.Add( "Destroying tracked GameObject failed: " + ex.Message );
                }
            }
            script.TrackedGameObjects.Clear();

            foreach ( var component in Resources.FindObjectsOfTypeAll<MonoBehaviour>() )
            {
                if ( component == null || component.GetType().Assembly != script.Assembly )
                {
                    continue;
                }
                if ( !component.gameObject.scene.IsValid() )
                {
                    continue;
                }
                try
                {
                    UnityEngine.Object.DestroyImmediate( component );
                    teardown.DestroyedComponents++;
                }
                catch ( Exception ex )
                {
                    teardown.Warnings.Add( "Destroying " + component.GetType().Name + " failed: " + ex.Message );
                }
            }

            foreach ( var warning in teardown.Warnings )
            {
                Main.Log( "[Script:" + name + "] teardown: " + warning );
            }

            return teardown;
        }

        private static object WithWarnings( object reply, List<string> warnings )
        {
            if ( warnings.Count == 0 )
            {
                return reply;
            }
            var withWarnings = JObject.FromObject( reply );
            withWarnings["warnings"] = new JArray( warnings.ToArray() );
            return withWarnings;
        }

        private static string InnerMessages( Exception ex )
        {
            return string.Concat( ScriptCompiler.ExceptionChain( ex ).Skip( 1 )
                .Select( inner => " <- " + inner.GetType().Name + ": " + inner.Message ).ToArray() );
        }

        private static string DescribeException( Exception ex )
        {
            var text = ex.GetType().FullName + ": " + ex.Message + InnerMessages( ex );
            return string.IsNullOrEmpty( ex.StackTrace ) ? text : text + "\n" + CodeExecutor.TrimStackTrace( ex.StackTrace );
        }

        private static object InvokeMain( string name, ActiveScript script, Dictionary<string, string> args,
            List<string> warnings )
        {
            script.Args = args ?? new Dictionary<string, string>();

            var mainMethod = FindEntryPoint( script.Assembly.GetTypes(), "Main" );
            if ( mainMethod == null )
            {
                return WithWarnings( new
                {
                    success = true,
                    executed = false,
                    scriptName = name,
                    message = "Script compiled and registered (no Main() found)",
                    types = script.Assembly.GetTypes().Select( t => t.FullName ).ToArray()
                }, warnings );
            }

            phase = "Main()";
            Exception executionException = null;
            var capture = new MainOutputCapture( script.Logger );
            bool finished;
            var saved = ScriptContext.Enter( script.HarmonyInstance, capture.Log,
                script.TrackedGameObjects, script.Args );
            try
            {
                finished = MainThreadDispatcher.RunStoppable( () =>
                {
                    try
                    {
                        mainMethod.Invoke( null, null );
                    }
                    catch ( TargetInvocationException tie )
                    {
                        executionException = tie.InnerException ?? tie;
                    }
                } );
            }
            catch ( Exception ex )
            {
                finished = true;
                executionException = ex;
            }
            finally
            {
                ScriptContext.Exit( saved );
            }

            var output = capture.Stop();

            const string StaysLoaded = " The script stays loaded with whatever Main() did before that, its patches " +
                                       "and objects included; unload_script tears it down.";

            if ( !finished )
            {
                return WithWarnings( new
                {
                    success = false,
                    loaded = true,
                    scriptName = name,
                    error = "Main() was stopped: it was still running after " + UserCodeBudgetMs / 1000 +
                            "s, so it was ended at its next loop iteration or method call." + StaysLoaded,
                    output
                }, warnings );
            }

            if ( executionException != null )
            {
                return WithWarnings( new
                {
                    success = false,
                    loaded = true,
                    scriptName = name,
                    error = "Main() threw an exception: " + executionException.Message + InnerMessages( executionException ) + "." + StaysLoaded,
                    stackTrace = CodeExecutor.TrimStackTrace( executionException.StackTrace ),
                    output
                }, warnings );
            }

            return WithWarnings( new
            {
                success = true,
                executed = true,
                scriptName = name,
                output
            }, warnings );
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
