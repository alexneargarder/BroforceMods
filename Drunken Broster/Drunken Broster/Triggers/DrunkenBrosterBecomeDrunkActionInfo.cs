using RocketLib.CustomTriggers;
using UnityEngine;

namespace Drunken_Broster.Triggers
{
    public class DrunkenBrosterBecomeDrunkActionInfo : CustomTriggerActionInfo
    {
        public bool becomeSober = false;

        public override void ShowGUI( LevelEditorGUI gui )
        {
            becomeSober = !GUILayout.Toggle( !becomeSober, "Make Drunk (Infinite)" );
            becomeSober = GUILayout.Toggle( becomeSober, "Become Sober" );
        }
    }
}
