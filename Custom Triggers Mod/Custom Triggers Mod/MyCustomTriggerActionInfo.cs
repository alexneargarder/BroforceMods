using UnityEngine;

namespace Custom_Triggers_Mod
{
    public class MyCustomTriggerActionInfo : CustomTriggerActionInfo
    {
        public override void ShowGUI( LevelEditorGUI gui )
        {
            GUILayout.Label( "HELLO WORLD" );
        }
    }
}
