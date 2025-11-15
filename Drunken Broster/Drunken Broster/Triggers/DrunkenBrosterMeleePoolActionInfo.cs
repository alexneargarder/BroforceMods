using RocketLib.CustomTriggers;
using UnityEngine;

namespace Drunken_Broster.Triggers
{
    public class DrunkenBrosterMeleePoolActionInfo : LevelStartTriggerActionInfo
    {
        public DrunkenBrosterMeleePoolActionInfo()
        {
            RunAtLevelStart = false;
        }

        public bool resetToDefault = false;
        public bool enableTire = false;
        public bool enableAcidEgg = false;
        public bool enableBeehive = false;
        public bool enableBottle = false;
        public bool enableCrate = false;
        public bool enableCoconut = false;
        public bool enableExplosiveBarrel = false;
        public bool enableSoccerBall = false;
        public bool enableAlienEgg = false;
        public bool enableSkull = false;

        public override void ShowGUI( LevelEditorGUI gui )
        {
            base.ShowGUI( gui );

            resetToDefault = GUILayout.Toggle( resetToDefault, "Reset to Default Pool" );

            if ( !resetToDefault )
            {
                GUILayout.Label( "Select items for pool:" );
                enableTire = GUILayout.Toggle( enableTire, "Tire" );
                enableAcidEgg = GUILayout.Toggle( enableAcidEgg, "Acid Egg" );
                enableBeehive = GUILayout.Toggle( enableBeehive, "Beehive" );
                enableBottle = GUILayout.Toggle( enableBottle, "Bottle" );
                enableCrate = GUILayout.Toggle( enableCrate, "Crate" );
                enableCoconut = GUILayout.Toggle( enableCoconut, "Coconut" );
                enableExplosiveBarrel = GUILayout.Toggle( enableExplosiveBarrel, "Explosive Barrel" );
                enableSoccerBall = GUILayout.Toggle( enableSoccerBall, "Soccer Ball" );
                enableAlienEgg = GUILayout.Toggle( enableAlienEgg, "Alien Egg" );
                enableSkull = GUILayout.Toggle( enableSkull, "Skull" );
            }
        }
    }
}
