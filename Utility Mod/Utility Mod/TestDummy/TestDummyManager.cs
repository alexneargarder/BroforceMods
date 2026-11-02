using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Utility_Mod
{
    public static class TestDummyManager
    {
        private static readonly List<TestDummy> activeDummies = new List<TestDummy>();
        private static GameObject hiddenRoot;

        private static GameObject GetHiddenRoot()
        {
            if ( hiddenRoot == null )
            {
                hiddenRoot = new GameObject( "__TestDummyHiddenRoot" );
                hiddenRoot.SetActive( false );
                UnityEngine.Object.DontDestroyOnLoad( hiddenRoot );
            }
            return hiddenRoot;
        }

        public static TestDummy SpawnTestDummy( Vector3 position )
        {
            TestVanDammeAnim mookPrefab = Map.Instance != null && Map.Instance.activeTheme != null
                ? Map.Instance.activeTheme.mook
                : null;
            if ( mookPrefab == null )
            {
                Main.mod.Logger.Log( "Error: No mook prefab in active theme" );
                return null;
            }

            // Instantiating under an inactive parent defers Awake until the Mook component is swapped for TestDummy.
            GameObject root = GetHiddenRoot();
            GameObject clone = UnityEngine.Object.Instantiate( mookPrefab.gameObject, root.transform );

            Mook originalMook = clone.GetComponent<Mook>();
            if ( originalMook == null )
            {
                Main.mod.Logger.Log( "Error: Mook prefab clone has no Mook component" );
                UnityEngine.Object.Destroy( clone );
                return null;
            }

            TestDummy dummy = clone.AddComponent<TestDummy>();
            CopyComponentFields( originalMook, dummy );

            // Destroy is deferred, so sibling Awakes would still find the original Mook through GetComponent.
            UnityEngine.Object.DestroyImmediate( originalMook );

            clone.transform.SetParent( null, false );
            // Unparenting alone leaves the clone in the DontDestroyOnLoad scene, which level teardown never reaches.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                clone, UnityEngine.SceneManagement.SceneManager.GetActiveScene() );
            clone.transform.position = position;
            clone.name = "TestDummy";
            clone.SetActive( true );
            // Map.HitUnits checks unit.X/Y, not transform.
            dummy.SetXY( position.x, position.y );

            dummy.Setup( position );

            activeDummies.Add( dummy );
            return dummy;
        }

        private static void CopyComponentFields( Component src, Component dst )
        {
            Type stop = typeof( MonoBehaviour );
            Type t = src.GetType();
            while ( t != null && t != stop && t != typeof( Behaviour ) && t != typeof( Component ) && t != typeof( UnityEngine.Object ) )
            {
                FieldInfo[] fields = t.GetFields( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly );
                for ( int i = 0; i < fields.Length; i++ )
                {
                    FieldInfo f = fields[i];
                    if ( f.IsLiteral || f.IsInitOnly ) continue;
                    try
                    {
                        f.SetValue( dst, f.GetValue( src ) );
                    }
                    catch
                    {
                        // Unity-managed fields can refuse the copy.
                    }
                }
                t = t.BaseType;
            }
        }

        public static List<TestDummy> GetActiveDummies()
        {
            activeDummies.RemoveAll( d => d == null );
            return activeDummies;
        }

        public static void SetDPSOverlayVisibility( bool visible )
        {
            foreach ( var dummy in GetActiveDummies() )
            {
                if ( dummy != null )
                {
                    dummy.showDPSOverlay = visible;
                }
            }
        }

        public static void RemoveAllDummies()
        {
            foreach ( var dummy in activeDummies )
            {
                if ( dummy != null )
                {
                    // Map.units is null during level teardown.
                    if ( Map.units != null )
                    {
                        Map.units.Remove( dummy );
                    }
                    UnityEngine.Object.Destroy( dummy.gameObject );
                }
            }
            activeDummies.Clear();
        }

        public static void ResetAllDummies()
        {
            foreach ( var dummy in GetActiveDummies() )
            {
                if ( dummy != null )
                {
                    dummy.ResetStats();
                    dummy.RestoreState();
                }
            }
        }

        public static void SetAllDummiesMode( TestDummyMode newMode )
        {
            foreach ( var dummy in GetActiveDummies() )
            {
                if ( dummy != null )
                {
                    dummy.SetMode( newMode );
                }
            }
        }

        public static bool AllDummiesInMode( TestDummyMode checkMode )
        {
            var dummies = GetActiveDummies();
            if ( dummies.Count == 0 ) return false;
            foreach ( var dummy in dummies )
            {
                if ( dummy != null && dummy.mode != checkMode ) return false;
            }
            return true;
        }
    }
}
