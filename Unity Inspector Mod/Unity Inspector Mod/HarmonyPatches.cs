using System;
using HarmonyLib;

namespace Unity_Inspector_Mod
{
    [HarmonyPatch( typeof( InputReader ), "GetMenuInputCombined" )]
    internal static class InputReader_GetMenuInputCombined_Patch
    {
        private static void Postfix( ref bool up, ref bool down, ref bool left,
            ref bool right, ref bool accept, ref bool decline, ref bool __result )
        {
            if ( !InputSimulator.Active || InputReader.IsBlocked )
            {
                return;
            }

            up |= InputSimulator.HeldAny( InputSimulator.Up );
            down |= InputSimulator.HeldAny( InputSimulator.Down );
            left |= InputSimulator.HeldAny( InputSimulator.Left );
            right |= InputSimulator.HeldAny( InputSimulator.Right );
            accept |= InputSimulator.HeldAny( InputSimulator.Fire ) | InputSimulator.HeldAny( InputSimulator.Jump );
            decline |= InputSimulator.HeldAny( InputSimulator.Special ) | ( InputSimulator.PressedMenuController( InputSimulator.Escape ) >= 0 );
            __result = up | down | left | right | accept | decline;
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetInput" )]
    [HarmonyPatch( new Type[]
        {
            typeof( int ), typeof( bool ), typeof( bool ), typeof( bool ), typeof( bool ),
            typeof( bool ), typeof( bool ), typeof( bool ), typeof( bool ), typeof( bool ), typeof( bool ),
            typeof( bool ), typeof( bool )
        },
        new ArgumentType[]
        {
            ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Ref,
            ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Ref,
            ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal
        } )]
    internal static class InputReader_GetInput_Patch
    {
        private static void Postfix( int controllerNum, ref bool up, ref bool down, ref bool left,
            ref bool right, ref bool fire, ref bool buttonJump, ref bool special, ref bool highFive,
            ref bool buttonGesture, ref bool sprint, bool isInChat, bool allowInCutscene )
        {
            if ( !InputSimulator.Active )
            {
                return;
            }
            if ( !CutsceneController.PlayersCanMove() || InputReader.IsBlocked
                                                      || ( CutsceneController.InCutscene && !allowInCutscene ) || PauseController.DelayInput )
            {
                return;
            }

            up |= InputSimulator.HeldOn( InputSimulator.Up, controllerNum );
            down |= InputSimulator.HeldOn( InputSimulator.Down, controllerNum );
            left |= InputSimulator.HeldOn( InputSimulator.Left, controllerNum );
            right |= InputSimulator.HeldOn( InputSimulator.Right, controllerNum );
            fire |= InputSimulator.HeldOn( InputSimulator.Fire, controllerNum );
            buttonJump |= InputSimulator.HeldOn( InputSimulator.Jump, controllerNum );
            special |= InputSimulator.HeldOn( InputSimulator.Special, controllerNum );
            highFive |= InputSimulator.HeldOn( InputSimulator.HighFive, controllerNum );
            buttonGesture |= InputSimulator.HeldOn( InputSimulator.Gesture, controllerNum );
            sprint |= InputSimulator.HeldOn( InputSimulator.Sprint, controllerNum );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetMenuInput" )]
    internal static class InputReader_GetMenuInput_Patch
    {
        private static void Postfix( int Controller, ref bool up, ref bool down, ref bool left,
            ref bool right, ref bool accept, ref bool decline, ref bool __result )
        {
            if ( !InputSimulator.Active || InputReader.IsBlocked )
            {
                return;
            }

            up |= InputSimulator.HeldOnMenu( InputSimulator.Up, Controller );
            down |= InputSimulator.HeldOnMenu( InputSimulator.Down, Controller );
            left |= InputSimulator.HeldOnMenu( InputSimulator.Left, Controller );
            right |= InputSimulator.HeldOnMenu( InputSimulator.Right, Controller );
            accept |= InputSimulator.HeldOnMenu( InputSimulator.Fire, Controller ) | InputSimulator.HeldOnMenu( InputSimulator.Jump, Controller );
            decline |= InputSimulator.HeldOnMenu( InputSimulator.Special, Controller ) | InputSimulator.PressedOnMenu( InputSimulator.Escape, Controller );
            __result = up | down | left | right | accept | decline;
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetControllerPressingStart" )]
    [HarmonyPatch( new Type[] { typeof( bool ), typeof( bool ) } )]
    internal static class InputReader_GetControllerPressingStart_Patch
    {
        private static void Postfix( ref int __result, bool isInChat, bool ignoreBlock )
        {
            if ( __result != -1 || !InputSimulator.Active )
            {
                return;
            }
            if ( InputReader.IsBlocked && !isInChat && !ignoreBlock )
            {
                return;
            }
            __result = InputSimulator.PressedMenuController( InputSimulator.Start );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetControllerPressingCancel" )]
    internal static class InputReader_GetControllerPressingCancel_Patch
    {
        private static void Postfix( ref int __result )
        {
            if ( __result != -1 || !InputSimulator.Active )
            {
                return;
            }
            __result = InputSimulator.PressedMenuController( InputSimulator.Escape );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetControllerPressingFire" )]
    [HarmonyPatch( new Type[] { typeof( bool ) } )]
    internal static class InputReader_GetControllerPressingFire_Patch
    {
        private static void Postfix( ref int __result, bool ignoreBlock )
        {
            if ( __result != -1 || !InputSimulator.Active || ( InputReader.IsBlocked && !ignoreBlock ) )
            {
                return;
            }
            __result = InputSimulator.PressedController( InputSimulator.Fire );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetControllerPressingHighFive" )]
    internal static class InputReader_GetControllerPressingHighFive_Patch
    {
        private static void Postfix( ref int __result )
        {
            if ( __result != -1 || !InputSimulator.Active || InputReader.IsBlocked )
            {
                return;
            }
            __result = InputSimulator.PressedController( InputSimulator.HighFive );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetControllerPressingJump" )]
    [HarmonyPatch( new Type[] { typeof( bool ) } )]
    internal static class InputReader_GetControllerPressingJump_Patch
    {
        private static void Postfix( ref int __result, bool ignoreBlock )
        {
            if ( __result != -1 || !InputSimulator.Active || ( InputReader.IsBlocked && !ignoreBlock ) )
            {
                return;
            }
            __result = InputSimulator.PressedController( InputSimulator.Jump );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetControllerPressingSpecial" )]
    internal static class InputReader_GetControllerPressingSpecial_Patch
    {
        private static void Postfix( ref int __result )
        {
            if ( __result != -1 || !InputSimulator.Active || InputReader.IsBlocked )
            {
                return;
            }
            __result = InputSimulator.PressedController( InputSimulator.Special );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetControllerPressingGesture" )]
    internal static class InputReader_GetControllerPressingGesture_Patch
    {
        private static void Postfix( ref int __result )
        {
            if ( __result != -1 || !InputSimulator.Active || InputReader.IsBlocked )
            {
                return;
            }
            __result = InputSimulator.HeldController( InputSimulator.Gesture );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "IsControllerPressingFire" )]
    internal static class InputReader_IsControllerPressingFire_Patch
    {
        private static void Postfix( int controllerNum, ref bool __result )
        {
            if ( __result || !InputSimulator.Active || InputReader.IsBlocked )
            {
                return;
            }
            __result = InputSimulator.PressedOn( InputSimulator.Fire, controllerNum );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "IsControllerPressingJump" )]
    internal static class InputReader_IsControllerPressingJump_Patch
    {
        private static void Postfix( int controllerNum, ref bool __result )
        {
            if ( __result || !InputSimulator.Active || InputReader.IsBlocked )
            {
                return;
            }
            __result = InputSimulator.PressedOn( InputSimulator.Jump, controllerNum );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "IsControllerPressingSpecial" )]
    internal static class InputReader_IsControllerPressingSpecial_Patch
    {
        private static void Postfix( int controllerNum, ref bool __result )
        {
            if ( __result || !InputSimulator.Active || InputReader.IsBlocked )
            {
                return;
            }
            __result = InputSimulator.PressedOn( InputSimulator.Special, controllerNum );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetControllerHoldingFire" )]
    internal static class InputReader_GetControllerHoldingFire_Patch
    {
        private static void Postfix( ref int __result )
        {
            if ( __result != -1 || !InputSimulator.Active || InputReader.IsBlocked )
            {
                return;
            }
            __result = InputSimulator.HeldController( InputSimulator.Fire );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetControllerHoldingJump" )]
    internal static class InputReader_GetControllerHoldingJump_Patch
    {
        private static void Postfix( ref int __result )
        {
            if ( __result != -1 || !InputSimulator.Active || InputReader.IsBlocked )
            {
                return;
            }
            __result = InputSimulator.HeldController( InputSimulator.Jump );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetControllerHoldingSpecial" )]
    internal static class InputReader_GetControllerHoldingSpecial_Patch
    {
        private static void Postfix( ref int __result )
        {
            if ( __result != -1 || !InputSimulator.Active || InputReader.IsBlocked )
            {
                return;
            }
            __result = InputSimulator.HeldController( InputSimulator.Special );
        }
    }

    [HarmonyPatch( typeof( InputReader ), "GetControllerHoldingMelee" )]
    internal static class InputReader_GetControllerHoldingMelee_Patch
    {
        private static void Postfix( ref int __result )
        {
            if ( __result != -1 || !InputSimulator.Active || InputReader.IsBlocked )
            {
                return;
            }
            __result = InputSimulator.HeldController( InputSimulator.HighFive );
        }
    }

    [HarmonyPatch( typeof( Utility.SceneLoader ), "LoadScene", new Type[] { typeof( string ), typeof( UnityEngine.SceneManagement.LoadSceneMode ) } )]
    internal static class SceneLoader_LoadScene_Patch
    {
        private static void Prefix( UnityEngine.SceneManagement.LoadSceneMode mode )
        {
            if ( mode == UnityEngine.SceneManagement.LoadSceneMode.Single )
            {
                InputSimulator.SceneLoadRequested();
            }
        }
    }
}
