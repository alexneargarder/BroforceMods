using System;
using System.Collections.Generic;
using UnityEngine;
using static Custom_Triggers_Mod.HarmonyPatches;

namespace Custom_Triggers_Mod
{
    public class CustomTriggerManager
    {
        public static List<CustomTrigger> CustomTriggers = new List<CustomTrigger>();
        static TriggerActionInfo currentAction;
        static string currentActionName;

        public static void RegisterCustomTrigger( Type customTriggerActionType, Type customTriggerActionInfoType, string actionName )
        {
            CustomTrigger customTrigger = new CustomTrigger( customTriggerActionType, customTriggerActionInfoType, actionName );
            CustomTriggers.Add( customTrigger );
        }

        public static void DisplayAddCustomTriggers( LevelEditorGUI __instance, ref TriggerInfo ___selectedTrigger, ref TriggerActionInfo ___selectedAction )
        {
            foreach ( CustomTrigger customTrigger in CustomTriggers )
            {
                if ( GUILayout.Button( "Add New " + customTrigger.ActionName + " Action", new GUILayoutOption[0] ) )
                {
                    LevelEditorGUI_ShowTriggerMenu_Patch.PlayClickSound( __instance );
                    TriggerActionInfo customActionInfo = Activator.CreateInstance( customTrigger.CustomTriggerActionInfoType ) as TriggerActionInfo;
                    customActionInfo.type = TriggerActionType.Weather;
                    ___selectedTrigger.actions.Add( customActionInfo );
                    ___selectedAction = customActionInfo;
                }
            }
        }

        public static string GetCustomActionType( TriggerActionInfo actionInfo )
        {
            if ( currentAction == actionInfo )
            {
                return currentActionName;
            }

            foreach ( CustomTrigger customTrigger in CustomTriggers )
            {
                if ( actionInfo.GetType() == customTrigger.CustomTriggerActionInfoType )
                {
                    currentActionName = customTrigger.ActionName;
                    currentAction = actionInfo;
                    return currentActionName;
                }
            }

            return string.Empty;
        }
    }
}
