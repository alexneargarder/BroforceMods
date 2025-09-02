using System.Collections.Generic;
using UnityEngine;

namespace Utility_Mod
{
    public static class TestDummyManager
    {
        private static GameObject testDummyPrefab;
        private static readonly List<TestDummy> activeDummies = new List<TestDummy>();

        public static GameObject GetTestDummyPrefab()
        {
            if ( testDummyPrefab == null )
            {
                CreateTestDummyPrefab();
            }
            return testDummyPrefab;
        }

        private static void CreateTestDummyPrefab()
        {
            TestVanDammeAnim mookPrefab = Map.Instance.activeTheme.mook;
            if ( mookPrefab == null )
            {
                Main.mod.Logger.Log( "Error: No mook prefab in active theme" );
                return;
            }

            GameObject originalGO = mookPrefab.gameObject;
            testDummyPrefab = UnityEngine.Object.Instantiate( originalGO );
            testDummyPrefab.SetActive( false );

            TestDummy dummy = testDummyPrefab.AddComponent<TestDummy>();

            dummy.Setup();

            UnityEngine.Object.DontDestroyOnLoad( testDummyPrefab );
        }

        public static TestDummy SpawnTestDummy( Vector3 position )
        {
            GameObject prefab = GetTestDummyPrefab();
            if ( prefab == null )
            {
                Main.mod.Logger.Log( "Error: Could not create test dummy prefab" );
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate( prefab, position, Quaternion.identity );
            instance.SetActive( true );

            TestDummy dummy = instance.GetComponent<TestDummy>();
            if ( dummy != null )
            {
                activeDummies.Add( dummy );
                dummy.Setup();

                if ( !Map.units.Contains( dummy ) )
                {
                    Map.units.Add( dummy );
                }

                Map.RegisterUnit( dummy, false );
            }

            return dummy;
        }

        public static void CleanupNullReferences()
        {
            activeDummies.RemoveAll( d => d == null );
        }

        public static List<TestDummy> GetActiveDummies()
        {
            CleanupNullReferences();
            return activeDummies;
        }


        public static void ToggleDPSDisplay()
        {
            foreach ( var dummy in GetActiveDummies() )
            {
                if ( dummy != null )
                {
                    dummy.showDPSOverlay = !dummy.showDPSOverlay;
                }
            }
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
                    Map.units.Remove( dummy );
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
                }
            }
        }
    }
}