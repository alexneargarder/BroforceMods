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
            
            if (InputSimulator.IsInputSimulated("up"))
                up = true;
            if (InputSimulator.IsInputSimulated("down"))
                down = true;
            if (InputSimulator.IsInputSimulated("left"))
                left = true;
            if (InputSimulator.IsInputSimulated("right"))
                right = true;
            if (InputSimulator.IsInputSimulated("fire"))
                fire = true;
            if (InputSimulator.IsInputSimulated("jump"))
                buttonJump = true;
            if (InputSimulator.IsInputSimulated("special"))
                special = true;
            if (InputSimulator.IsInputSimulated("highfive"))
                highFive = true;
            if (InputSimulator.IsInputSimulated("gesture"))
                buttonGesture = true;
            if (InputSimulator.IsInputSimulated("sprint"))
                sprint = true;
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
                __result = 4;
            }
        }
    }
    
    [HarmonyPatch(typeof(InputReader), "GetControllerPressingCancel")]
    static class InputReader_GetControllerPressingCancel_Patch
    {
        static void Postfix(ref int __result)
        {
            if (!InputSimulator.HasAnySimulatedInput()) return;
            
            if (InputSimulator.IsInputSimulated("escape") && __result == -1)
            {
                __result = 4;
            }
        }
    }
}