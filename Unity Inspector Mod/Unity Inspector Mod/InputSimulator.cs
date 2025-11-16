using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public static class InputSimulator
    {
        private static Dictionary<string, bool> simulatedInputs = new Dictionary<string, bool>();
        private static Dictionary<string, Timer> activeHolds = new Dictionary<string, Timer>();
        private static bool startButtonHandled = false;
        
        public static void Initialize()
        {
            simulatedInputs["up"] = false;
            simulatedInputs["down"] = false;
            simulatedInputs["left"] = false;
            simulatedInputs["right"] = false;
            simulatedInputs["fire"] = false;
            simulatedInputs["jump"] = false;
            simulatedInputs["special"] = false;
            simulatedInputs["highfive"] = false;
            simulatedInputs["gesture"] = false;
            simulatedInputs["sprint"] = false;
            simulatedInputs["start"] = false;
            simulatedInputs["escape"] = false;
        }
        
        public static object SimulateInput(string action, int? duration = null, int? playerNum = null, int? count = null, int? interval = null)
        {
            try
            {
                string inputKey = action.ToLower();
                
                if (!simulatedInputs.ContainsKey(inputKey))
                {
                    return new { success = false, error = $"Unknown input action: {action}" };
                }
                
                int targetPlayer = playerNum ?? 0;
                int pressCount = count ?? 1;
                int pressInterval = interval ?? 200; // Default 200ms between presses
                
                if (pressCount > 1)
                {
                    // Multiple presses
                    PressInputMultiple(inputKey, pressCount, pressInterval, targetPlayer);
                    return new { success = true, action = action, count = pressCount, interval = pressInterval, player = targetPlayer };
                }
                else if (duration.HasValue && duration.Value > 0)
                {
                    if (activeHolds.ContainsKey(inputKey))
                    {
                        activeHolds[inputKey].Dispose();
                        activeHolds.Remove(inputKey);
                    }
                    
                    HoldInput(inputKey, duration.Value, targetPlayer);
                    
                    return new { success = true, action = action, duration = duration.Value, player = targetPlayer };
                }
                else
                {
                    PressInput(inputKey, targetPlayer);
                    return new { success = true, action = action, player = targetPlayer };
                }
            }
            catch (Exception ex)
            {
                Main.Log($"Error simulating input: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }
        
        private static void HoldInput(string inputKey, int durationMs, int playerNum)
        {
            simulatedInputs[inputKey] = true;
            
            var timer = new Timer(_ =>
            {
                simulatedInputs[inputKey] = false;
                if (activeHolds.ContainsKey(inputKey))
                {
                    activeHolds[inputKey].Dispose();
                    activeHolds.Remove(inputKey);
                }
            }, null, durationMs, Timeout.Infinite);
            
            activeHolds[inputKey] = timer;
        }
        
        private static void PressInput(string inputKey, int playerNum)
        {
            simulatedInputs[inputKey] = true;
            
            var timer = new Timer(_ =>
            {
                simulatedInputs[inputKey] = false;
            }, null, 50, Timeout.Infinite);
        }
        
        private static void PressInputMultiple(string inputKey, int count, int intervalMs, int playerNum)
        {
            // Block until all presses complete
            for (int i = 0; i < count; i++)
            {
                // Press the input
                simulatedInputs[inputKey] = true;

                // Release after 50ms
                var releaseTimer = new Timer(_ =>
                {
                    simulatedInputs[inputKey] = false;
                }, null, 50, Timeout.Infinite);

                // Wait for interval before next press (except after last press)
                if (i < count - 1)
                {
                    System.Threading.Thread.Sleep(intervalMs);
                }
            }
        }
        
        public static bool IsInputSimulated(string inputKey)
        {
            return simulatedInputs.ContainsKey(inputKey) && simulatedInputs[inputKey];
        }
        
        public static bool HasAnySimulatedInput()
        {
            foreach (var kvp in simulatedInputs)
            {
                if (kvp.Value) return true;
            }
            return false;
        }
        
        public static bool ShouldTriggerStart()
        {
            if (simulatedInputs.ContainsKey("start") && simulatedInputs["start"] && !startButtonHandled)
            {
                startButtonHandled = true;
                // Reset the flag after a short delay to allow for next press
                var resetTimer = new Timer(_ =>
                {
                    startButtonHandled = false;
                }, null, 100, Timeout.Infinite);
                return true;
            }
            
            return false;
        }
        
        public static void ClearAllInputs()
        {
            foreach (var kvp in activeHolds)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.Dispose();
                }
            }
            activeHolds.Clear();
            
            foreach (var key in simulatedInputs.Keys)
            {
                simulatedInputs[key] = false;
            }
        }
        
        public static void Cleanup()
        {
            ClearAllInputs();
        }
    }
}