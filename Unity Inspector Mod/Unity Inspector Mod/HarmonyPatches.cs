using System;
using HarmonyLib;

namespace Unity_Inspector_Mod
{
    [HarmonyPatch(typeof(InputReader), "GetMenuInputCombined")]
    static class InputReader_GetMenuInputCombined_Patch
    {
        static void Postfix(ref bool up, ref bool down, ref bool left, 
            ref bool right, ref bool accept, ref bool decline)
        {
            if (!InputSimulator.HasAnySimulatedInput()) return;
            
            if (InputSimulator.IsInputSimulated("up"))
                up = true;
            if (InputSimulator.IsInputSimulated("down"))
                down = true;
            if (InputSimulator.IsInputSimulated("left"))
                left = true;
            if (InputSimulator.IsInputSimulated("right"))
                right = true;
            if (InputSimulator.IsInputSimulated("fire") || InputSimulator.IsInputSimulated("jump"))
                accept = true;
            if (InputSimulator.IsInputSimulated("special"))
                decline = true;
        }
    }
    
    [HarmonyPatch(typeof(InputReader), "GetInput")]
    [HarmonyPatch(new Type[] { typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool), 
        typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), 
        typeof(bool), typeof(bool) },
        new ArgumentType[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Ref, 
            ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Ref, 
            ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal })]
    static class InputReader_GetInput_Patch
    {
        static void Postfix(int controllerNum, ref bool up, ref bool down, ref bool left, 
            ref bool right, ref bool fire, ref bool buttonJump, ref bool special, ref bool highFive, 
            ref bool buttonGesture, ref bool sprint, bool isInChat, bool allowInCutscene)
        {
            if (!InputSimulator.HasAnySimulatedInput()) return;
            
            // A free controller reporting a press is a player dropping in.
            if (InputSimulator.IsSimulatedOn("up", controllerNum))
                up = true;
            if (InputSimulator.IsSimulatedOn("down", controllerNum))
                down = true;
            if (InputSimulator.IsSimulatedOn("left", controllerNum))
                left = true;
            if (InputSimulator.IsSimulatedOn("right", controllerNum))
                right = true;
            if (InputSimulator.IsSimulatedOn("fire", controllerNum))
                fire = true;
            if (InputSimulator.IsSimulatedOn("jump", controllerNum))
                buttonJump = true;
            if (InputSimulator.IsSimulatedOn("special", controllerNum))
                special = true;
            if (InputSimulator.IsSimulatedOn("highfive", controllerNum))
                highFive = true;
            if (InputSimulator.IsSimulatedOn("gesture", controllerNum))
                buttonGesture = true;
            if (InputSimulator.IsSimulatedOn("sprint", controllerNum))
                sprint = true;
        }
    }

    [HarmonyPatch(typeof(InputReader), "GetMenuInput")]
    static class InputReader_GetMenuInput_Patch
    {
        static void Postfix(int Controller, ref bool up, ref bool down, ref bool left,
            ref bool right, ref bool accept, ref bool decline, ref bool __result)
        {
            if (!InputSimulator.HasAnySimulatedInput()) return;

            if (InputSimulator.IsSimulatedOnMenu("up", Controller))
                up = true;
            if (InputSimulator.IsSimulatedOnMenu("down", Controller))
                down = true;
            if (InputSimulator.IsSimulatedOnMenu("left", Controller))
                left = true;
            if (InputSimulator.IsSimulatedOnMenu("right", Controller))
                right = true;
            if (InputSimulator.IsSimulatedOnMenu("fire", Controller) || InputSimulator.IsSimulatedOnMenu("jump", Controller))
                accept = true;
            if (InputSimulator.IsSimulatedOnMenu("special", Controller))
                decline = true;

            __result = up | down | left | right | accept | decline;
        }
    }
    
    [HarmonyPatch(typeof(InputReader), "GetControllerPressingStart")]
    [HarmonyPatch(new Type[] { typeof(bool), typeof(bool) })]
    static class InputReader_GetControllerPressingStart_Patch
    {
        static void Postfix(ref int __result, bool isInChat, bool ignoreBlock)
        {
            if (!InputSimulator.HasAnySimulatedInput()) return;
            
            if (InputSimulator.ShouldTriggerStart() && __result == -1)
            {
                __result = InputSimulator.MenuControllerPressing("start");
            }
        }
    }
    
    [HarmonyPatch(typeof(InputReader), "GetControllerPressingCancel")]
    static class InputReader_GetControllerPressingCancel_Patch
    {
        static void Postfix(ref int __result)
        {
            if (!InputSimulator.HasAnySimulatedInput()) return;

            if (__result == -1)
            {
                __result = InputSimulator.MenuControllerPressing("escape");
            }
        }
    }

    [HarmonyPatch(typeof(InputReader), "GetControllerPressingFire")]
    [HarmonyPatch(new Type[] { typeof(bool) })]
    static class InputReader_GetControllerPressingFire_Patch
    {
        static void Postfix(ref int __result, bool ignoreBlock)
        {
            if (!InputSimulator.HasAnySimulatedInput()) return;

            if (__result == -1)
            {
                __result = InputSimulator.ControllerPressing("fire");
            }
        }
    }

    [HarmonyPatch(typeof(InputReader), "GetControllerPressingHighFive")]
    static class InputReader_GetControllerPressingHighFive_Patch
    {
        static void Postfix(ref int __result)
        {
            if (!InputSimulator.HasAnySimulatedInput()) return;

            if (__result == -1)
            {
                __result = InputSimulator.ControllerPressing("highfive");
            }
        }
    }

    [HarmonyPatch(typeof(InputReader), "GetControllerPressingJump")]
    [HarmonyPatch(new Type[] { typeof(bool) })]
    static class InputReader_GetControllerPressingJump_Patch
    {
        static void Postfix(ref int __result, bool ignoreBlock)
        {
            if (!InputSimulator.HasAnySimulatedInput()) return;

            if (__result == -1)
            {
                __result = InputSimulator.ControllerPressing("jump");
            }
        }
    }

    [HarmonyPatch(typeof(InputReader), "GetControllerPressingSpecial")]
    static class InputReader_GetControllerPressingSpecial_Patch
    {
        static void Postfix(ref int __result)
        {
            if (!InputSimulator.HasAnySimulatedInput()) return;

            if (__result == -1)
            {
                __result = InputSimulator.ControllerPressing("special");
            }
        }
    }
}