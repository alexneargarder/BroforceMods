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

        public bool resetToDefault;
        public bool enableTire;
        public bool enableAcidEgg;
        public bool enableBeehive;
        public bool enableBottle;
        public bool enableCrate;
        public bool enableCoconut;
        public bool enableExplosiveBarrel;
        public bool enableSoccerBall;
        public bool enableAlienEgg;
        public bool enableSkull;

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
