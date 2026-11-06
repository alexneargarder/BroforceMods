using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    internal static class FrozenClock
    {
        private static readonly MethodInfo DivisorMethod = AccessTools.Method( typeof( FrozenClock ), "Divisor" );

        public static void Install( Harmony harmony )
        {
            Guard( harmony, typeof( BroLee ), "SetDeltaTime", Type.EmptyTypes, 2, "Bro Lee's position during his special" );
            Guard( harmony, typeof( BroLee ), "Update", Type.EmptyTypes, 1, "Bro Lee's attack trail" );
            Guard( harmony, typeof( TestVanDammeAnim ), "Jump", new Type[] { typeof( bool ) }, 2, "the position of a bro that jumps" );
            Guard( harmony, typeof( SortOfFollow ), "LateUpdate", Type.EmptyTypes, 1, "the camera shake" );
            Guard( harmony, typeof( StatisticsController ), "Update", Type.EmptyTypes, 1, "the level's time" );
            Guard( harmony, typeof( FallingBlock ), "DamageInternal", new Type[] { typeof( int ), typeof( float ), typeof( float ) }, 2, "the push of a crate, barrel or other falling block" );
        }

        private static void Guard( Harmony harmony, Type type, string name, Type[] parameters, int divisions, string keeps )
        {
            string warning = "set_game_speed 0 can turn " + keeps + " NaN for good";
            try
            {
                MethodInfo method = AccessTools.DeclaredMethod( type, name, parameters );
                int found = method == null ? 0 : CountClockDivisions( PatchProcessor.GetOriginalInstructions( method ) );
                if ( found != divisions )
                {
                    Main.Log( type.Name + "." + name + " divides by the clock " + found + " times, expected " + divisions + " - " + warning );
                    return;
                }
                harmony.Patch( method, transpiler: new HarmonyMethod( typeof( FrozenClock ).GetMethod( "Transpiler", BindingFlags.Static | BindingFlags.NonPublic ) ) );
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch " + type.Name + "." + name + " (" + e.GetType().Name + ": " + e.Message + ") - " + warning );
            }
        }

        private static int CountClockDivisions( List<CodeInstruction> instructions )
        {
            int count = 0;
            for ( int i = 1; i < instructions.Count; i++ )
            {
                if ( instructions[i].opcode == OpCodes.Div && LoadsClock( instructions[i - 1] ) )
                {
                    count++;
                }
            }
            return count;
        }

        private static bool LoadsClock( CodeInstruction instruction )
        {
            MethodInfo method = instruction.operand as MethodInfo;
            if ( method != null )
            {
                return method.DeclaringType == typeof( Time ) && ( method.Name == "get_timeScale" || method.Name == "get_deltaTime" );
            }
            FieldInfo field = instruction.operand as FieldInfo;
            return field != null && instruction.opcode == OpCodes.Ldfld && field.Name == "t" && field.FieldType == typeof( float );
        }

        private static IEnumerable<CodeInstruction> Transpiler( IEnumerable<CodeInstruction> instructions )
        {
            CodeInstruction previous = null;
            foreach ( CodeInstruction instruction in instructions )
            {
                if ( previous != null && instruction.opcode == OpCodes.Div && LoadsClock( previous ) )
                {
                    yield return new CodeInstruction( OpCodes.Call, DivisorMethod );
                }
                yield return instruction;
                previous = instruction;
            }
        }

        private static float Divisor( float clock )
        {
            return clock != 0f ? clock : float.PositiveInfinity;
        }
    }
}
