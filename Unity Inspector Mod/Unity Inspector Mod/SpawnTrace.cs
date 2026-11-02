using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    // Everything here runs on the main thread: the prefixes because the game calls its spawn methods
    // there, the rest through MainThreadDispatcher. That is what orders a prefix's write against a read.
    // No [HarmonyPatch] attributes here, so Main's PatchAll never installs these prefixes.
    internal static class SpawnTrace
    {
        private const int RingCapacity = 512;

        private const string TargetMookNetworked = "MapController.SpawnMook_Networked";
        private const string TargetMookLocal = "MapController.SpawnMook_Local";
        private const string TargetProjectile = "ProjectileController.SpawnProjectileOverNetwork";
        private const string TargetDoodad = "Map.PlaceDoodad";

        private static readonly string[] AllTargets = { TargetMookNetworked, TargetMookLocal, TargetProjectile, TargetDoodad };

        // PlaceDoodad is opt-in: map generation and sandstorm debris call it continuously.
        private static readonly string[] DefaultTargets = { TargetMookNetworked, TargetMookLocal, TargetProjectile };

        private static readonly Type[] MookSpawnParameters =
        {
            typeof( Mook ), typeof( float ), typeof( float ), typeof( float ), typeof( float ),
            typeof( bool ), typeof( bool ), typeof( bool ), typeof( bool ), typeof( bool )
        };

        private static readonly Type[] ProjectileSpawnParameters =
        {
            typeof( Projectile ), typeof( MonoBehaviour ), typeof( float ), typeof( float ), typeof( float ), typeof( float ),
            typeof( bool ), typeof( int ), typeof( bool ), typeof( bool ), typeof( float )
        };

        private sealed class Ring
        {
            public readonly Queue<KeyValuePair<long, object>> Records = new Queue<KeyValuePair<long, object>>();
            public long Dropped;
        }

        private static Harmony harmony;
        private static string harmonyId;
        private static readonly Dictionary<string, Ring> rings = new Dictionary<string, Ring>();
        private static readonly List<string> patched = new List<string>();
        private static long seq;
        private static object owner;
        private static bool recordFailureLogged;

        public static string HarmonyId
        {
            get
            {
                if ( harmonyId == null )
                {
                    string modId = Main.mod != null && Main.mod.Info != null ? Main.mod.Info.Id : "UnityInspectorMod";
                    harmonyId = modId + ".trace-spawns";
                }
                return harmonyId;
            }
        }

        public static object Start( string[] requested, object client )
        {
            string[] targets = requested ?? DefaultTargets;

            string[] unknown = targets.Where( t => !AllTargets.Contains( t ) ).ToArray();
            if ( unknown.Length > 0 || targets.Length == 0 )
            {
                return new
                {
                    success = false,
                    error = unknown.Length > 0
                        ? $"Unknown trace target(s): {string.Join( ", ", unknown )}"
                        : "targets is empty - name at least one target, or omit it for the default set",
                    validTargets = AllTargets
                };
            }

            targets = targets.Distinct().ToArray();

            bool replacing = patched.Count > 0;
            string[] previous = patched.ToArray();

            if ( !replacing )
            {
                rings.Clear();
            }

            Unpatch();

            if ( harmony == null )
            {
                harmony = new Harmony( HarmonyId );
            }

            foreach ( string target in targets )
            {
                MethodBase original = ResolveOriginal( target );
                string failure = null;

                if ( original == null )
                {
                    failure = "the method was not found in this game build";
                }
                else
                {
                    try
                    {
                        // Ahead of every other prefix, so a mod prefix that skips the original cannot hide the call.
                        var prefix = new HarmonyMethod( AccessTools.Method( typeof( SpawnTrace ), PrefixName( target ) ) )
                        {
                            priority = int.MaxValue
                        };
                        harmony.Patch( original, prefix: prefix );
                    }
                    catch ( Exception ex )
                    {
                        failure = ex.Message;
                    }
                }

                if ( failure != null )
                {
                    Unpatch();
                    owner = null;
                    return new
                    {
                        success = false,
                        error = $"Patching {target} failed: {failure} - nothing is traced now",
                        tracing = false
                    };
                }

                patched.Add( target );
                if ( !rings.ContainsKey( target ) )
                {
                    rings[target] = new Ring();
                }
            }

            owner = client;

            return new
            {
                success = true,
                tracing = true,
                targets = patched.ToArray(),
                seq,
                replaced = replacing,
                previousTargets = replacing ? previous : null,
                note = replacing ? "Replaced the traced set; the records so far and the seq counter are kept" : null
            };
        }

        public static object Read( long since )
        {
            var records = rings.Values
                .SelectMany( ring => ring.Records )
                .Where( record => record.Key > since )
                .OrderBy( record => record.Key )
                .Select( record => record.Value )
                .ToArray();

            var dropped = new Dictionary<string, long>();
            foreach ( var entry in rings )
            {
                dropped[entry.Key] = entry.Value.Dropped;
            }

            return new
            {
                success = true,
                tracing = patched.Count > 0,
                targets = patched.ToArray(),
                seq,
                records,
                dropped
            };
        }

        public static object Stop()
        {
            bool wasTracing = patched.Count > 0;
            string[] previous = patched.ToArray();

            Unpatch();
            rings.Clear();
            owner = null;

            return new
            {
                success = true,
                tracing = false,
                stopped = wasTracing,
                targets = previous
            };
        }

        // Called on the client's receive thread; Harmony must not run there, so the unpatch is queued.
        public static void ReleaseOwner( object client )
        {
            if ( client == null )
            {
                return;
            }

            MainThreadDispatcher.Enqueue( () =>
            {
                if ( owner != client || patched.Count == 0 )
                {
                    return;
                }

                owner = null;
                Unpatch();
                Main.Log( "[SpawnTrace] The client that started trace_spawns disconnected - unpatched, records kept until the next start or stop" );
            } );
        }

        private static void Unpatch()
        {
            if ( harmony != null )
            {
                harmony.UnpatchAll( HarmonyId );
            }
            patched.Clear();
        }

        private static MethodBase ResolveOriginal( string target )
        {
            switch ( target )
            {
                case TargetMookNetworked:
                    return AccessTools.Method( typeof( MapController ), "SpawnMook_Networked", MookSpawnParameters );
                case TargetMookLocal:
                    return AccessTools.Method( typeof( MapController ), "SpawnMook_Local", MookSpawnParameters );
                case TargetProjectile:
                    return AccessTools.Method( typeof( ProjectileController ), "SpawnProjectileOverNetwork", ProjectileSpawnParameters );
                case TargetDoodad:
                    return AccessTools.Method( typeof( Map ), "PlaceDoodad", new[] { typeof( DoodadInfo ) } );
                default:
                    return null;
            }
        }

        private static string PrefixName( string target )
        {
            switch ( target )
            {
                case TargetMookNetworked: return nameof( SpawnMookNetworkedPrefix );
                case TargetMookLocal: return nameof( SpawnMookLocalPrefix );
                case TargetProjectile: return nameof( SpawnProjectilePrefix );
                default: return nameof( PlaceDoodadPrefix );
            }
        }

        private static void Record( string target, Func<long, object> build )
        {
            try
            {
                Ring ring;
                if ( !rings.TryGetValue( target, out ring ) )
                {
                    return;
                }

                long next = ++seq;
                ring.Records.Enqueue( new KeyValuePair<long, object>( next, build( next ) ) );
                if ( ring.Records.Count > RingCapacity )
                {
                    ring.Records.Dequeue();
                    ring.Dropped++;
                }
            }
            catch ( Exception ex )
            {
                if ( !recordFailureLogged )
                {
                    recordFailureLogged = true;
                    Main.Log( $"[SpawnTrace] Recording a {target} call failed: {ex}" );
                }
            }
        }

        // Each prefix declares only its own target's parameters: Harmony rejects a name the original lacks.
        private static void SpawnMookNetworkedPrefix( Mook mookPrefab, float x, float y, float xI, float yI )
        {
            RecordMook( TargetMookNetworked, mookPrefab, x, y, xI, yI );
        }

        private static void SpawnMookLocalPrefix( Mook mookPrefab, float x, float y, float xI, float yI )
        {
            RecordMook( TargetMookLocal, mookPrefab, x, y, xI, yI );
        }

        private static void RecordMook( string method, Mook mookPrefab, float x, float y, float xI, float yI )
        {
            Record( method, s => new
            {
                seq = s,
                method,
                prefab = mookPrefab == null ? null : mookPrefab.name,
                x,
                y,
                xI,
                yI,
                time = Time.realtimeSinceStartup
            } );
        }

        private static void SpawnProjectilePrefix( Projectile prefab, MonoBehaviour FiredBy, float x, float y, float xI, float yI, int playerNum )
        {
            Record( TargetProjectile, s => new
            {
                seq = s,
                method = TargetProjectile,
                prefab = prefab == null ? null : prefab.name,
                firedBy = ReferenceEquals( FiredBy, null ) ? null : FiredBy.GetType().Name,
                x,
                y,
                xI,
                yI,
                playerNum,
                time = Time.realtimeSinceStartup
            } );
        }

        private static void PlaceDoodadPrefix( DoodadInfo doodad )
        {
            if ( doodad == null )
            {
                return;
            }

            GridPoint position = doodad.position;
            Record( TargetDoodad, s => new
            {
                seq = s,
                method = TargetDoodad,
                doodadType = doodad.type.ToString(),
                gridPosition = ReferenceEquals( position, null ) ? null : new { x = position.collumn, y = position.row },
                variation = doodad.variation,
                time = Time.realtimeSinceStartup
            } );
        }
    }
}
