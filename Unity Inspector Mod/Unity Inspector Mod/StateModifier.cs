using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Unity_Inspector_Mod
{
    public static class StateModifier
    {
        public static object ModifyComponent(string gameObjectPath, string componentType, JObject properties)
        {
            try
            {
                GameObject go = FindGameObjectByPath(gameObjectPath);
                if (go == null)
                {
                    return new { success = false, error = $"GameObject not found at path: {gameObjectPath}" };
                }

                Component component = null;
                
                // Try to find component by type name
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp.GetType().Name == componentType || comp.GetType().FullName == componentType)
                    {
                        component = comp;
                        break;
                    }
                }

                if (component == null)
                {
                    return new { success = false, error = $"Component {componentType} not found on {gameObjectPath}" };
                }

                var modifiedProperties = new List<string>();
                var errors = new List<string>();

                foreach (var prop in properties)
                {
                    string propName = prop.Key;
                    JToken propValue = prop.Value;

                    if (SetPropertyValue(component, propName, propValue))
                    {
                        modifiedProperties.Add(propName);
                    }
                    else
                    {
                        errors.Add($"Failed to set {propName}");
                    }
                }

                return new
                {
                    success = true,
                    modifiedProperties = modifiedProperties.ToArray(),
                    errors = errors.ToArray(),
                    component = componentType,
                    gameObject = gameObjectPath
                };
            }
            catch (Exception ex)
            {
                Main.Log($"ModifyComponent error: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }

        private static bool SetPropertyValue(object target, string propertyName, JToken value)
        {
            try
            {
                Type type = target.GetType();
                
                // Try field first
                FieldInfo field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    object convertedValue = ConvertValue(value, field.FieldType);
                    field.SetValue(target, convertedValue);
                    return true;
                }

                // Try property
                PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    object convertedValue = ConvertValue(value, property.PropertyType);
                    property.SetValue(target, convertedValue, null);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Main.Log($"SetPropertyValue error for {propertyName}: {ex.Message}");
                return false;
            }
        }

        private static object ConvertValue(JToken value, Type targetType)
        {
            if (value == null || value.Type == JTokenType.Null)
                return null;

            // Handle Unity types
            if (targetType == typeof(Vector3))
            {
                if (value.Type == JTokenType.Object)
                {
                    return new Vector3(
                        value["x"]?.Value<float>() ?? 0,
                        value["y"]?.Value<float>() ?? 0,
                        value["z"]?.Value<float>() ?? 0
                    );
                }
            }
            else if (targetType == typeof(Vector2))
            {
                if (value.Type == JTokenType.Object)
                {
                    return new Vector2(
                        value["x"]?.Value<float>() ?? 0,
                        value["y"]?.Value<float>() ?? 0
                    );
                }
            }
            else if (targetType == typeof(Color))
            {
                if (value.Type == JTokenType.Object)
                {
                    return new Color(
                        value["r"]?.Value<float>() ?? 0,
                        value["g"]?.Value<float>() ?? 0,
                        value["b"]?.Value<float>() ?? 0,
                        value["a"]?.Value<float>() ?? 1
                    );
                }
            }
            else if (targetType == typeof(Quaternion))
            {
                if (value.Type == JTokenType.Object)
                {
                    // Support both quaternion and euler angles
                    if (value["euler"] != null)
                    {
                        var euler = value["euler"];
                        return Quaternion.Euler(
                            euler["x"]?.Value<float>() ?? 0,
                            euler["y"]?.Value<float>() ?? 0,
                            euler["z"]?.Value<float>() ?? 0
                        );
                    }
                    else
                    {
                        return new Quaternion(
                            value["x"]?.Value<float>() ?? 0,
                            value["y"]?.Value<float>() ?? 0,
                            value["z"]?.Value<float>() ?? 0,
                            value["w"]?.Value<float>() ?? 1
                        );
                    }
                }
            }

            // Use JSON.NET's built-in conversion for other types
            return value.ToObject(targetType);
        }

        private static GameObject FindGameObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            // Handle root path
            if (path == "/")
                return null;

            string[] parts = path.TrimStart('/').Split('/');
            
            // Find root objects
            GameObject[] rootObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            GameObject current = null;

            // Find the root object
            foreach (var obj in rootObjects)
            {
                if (obj.transform.parent == null && obj.name == parts[0])
                {
                    current = obj;
                    break;
                }
            }

            if (current == null)
                return null;

            // Traverse the path
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.transform.Find(parts[i]);
                if (child == null)
                    return null;
                current = child.gameObject;
            }

            return current;
        }

        // High-level convenience methods
        public static object TeleportPlayer(float x, float y, float? z = null)
        {
            try
            {
                var players = HeroController.players;
                if (players == null || players.Length == 0 || players[0]?.character == null)
                {
                    return new { success = false, error = "No player found" };
                }

                var player = players[0].character;
                Vector3 newPosition = new Vector3(x, y, z ?? player.transform.position.z);
                player.transform.position = newPosition;
                
                // Also update the character's internal position tracking
                player.X = x;
                player.Y = y;

                return new { 
                    success = true, 
                    newPosition = new { x = x, y = y, z = newPosition.z },
                    playerName = player.name
                };
            }
            catch (Exception ex)
            {
                Main.Log($"TeleportPlayer error: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }

        public static object SetPlayerHealth(int health)
        {
            try
            {
                var players = HeroController.players;
                if (players == null || players.Length == 0 || players[0]?.character == null)
                {
                    return new { success = false, error = "No player found" };
                }

                var player = players[0].character;
                player.health = health;
                
                // Make sure the player isn't dead if health > 0
                if (health > 0 && player.actionState == ActionState.Dead)
                {
                    player.actionState = ActionState.Idle;
                }

                return new { 
                    success = true, 
                    health = health,
                    playerName = player.name
                };
            }
            catch (Exception ex)
            {
                Main.Log($"SetPlayerHealth error: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }

        public static object SpawnEntity(string entityType, float x, float y)
        {
            try
            {
                // This is a placeholder - actual spawning would require finding the prefab
                // and instantiating it, which is complex in Broforce
                
                
                // For now, we can at least report what entities are available
                var availableTypes = new List<string>();
                
                // Find all prefabs in resources
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.name.Contains("Mook") || obj.name.Contains("Enemy") || 
                        obj.name.Contains("Pickup") || obj.name.Contains("Crate"))
                    {
                        if (!availableTypes.Contains(obj.name))
                            availableTypes.Add(obj.name);
                    }
                }

                return new { 
                    success = false, 
                    error = "Spawning not yet implemented",
                    availableTypes = availableTypes.ToArray(),
                    note = "Use execute_code to spawn entities manually for now"
                };
            }
            catch (Exception ex)
            {
                Main.Log($"SpawnEntity error: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }

        public static object SetGameSpeed(float speed)
        {
            try
            {
                Time.timeScale = speed;
                return new { success = true, timeScale = speed };
            }
            catch (Exception ex)
            {
                Main.Log($"SetGameSpeed error: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }
        
        public static object ListCampaigns()
        {
            try
            {
                string[] campaignNames = new string[]
                {
                    "WM_Intro(mouse)",
                    "WM_Mission1(mouse)",
                    "WM_Mission2 (mouse)",
                    "WM_City1(mouse)",
                    "WM_Bombardment(mouse)",
                    "WM_Village1(mouse)",
                    "WM_City2(mouse)",
                    "WM_Bombardment2(mouse)",
                    "WM_KazakhstanIndustrial(mouse)",
                    "WM_KazakhstanRainy(mouse)",
                    "WM_AlienMission1(mouse)",
                    "WM_AlienMission2(mouse)",
                    "WM_AlienMission3(mouse)",
                    "WM_AlienMission4(mouse)",
                    "WM_HELL",
                    "WM_Whitehouse",
                    "Challenge_Alien",
                    "Challenge_Bombardment1",
                    "Challenge_Ammo",
                    "Challenge_Dash",
                    "Challenge_Mech",
                    "Challenge_MacBrover",
                    "Challenge_TimeBro",
                    "MuscleTemple_5Stages1",
                    "MuscleTemple_5Stages2",
                    "MuscleTemple_5Stages3",
                    "MuscleTemple_5Stages4",
                    "MuscleTemple_5Stages5"
                };

                var campaigns = new List<object>();
                for (int i = 0; i < campaignNames.Length; i++)
                {
                    campaigns.Add(new
                    {
                        index = i,
                        name = campaignNames[i],
                        displayName = campaignNames[i].Replace("WM_", "").Replace("(mouse)", "").Replace("_", " ")
                    });
                }

                return new { success = true, campaigns = campaigns };
            }
            catch (Exception ex)
            {
                Main.Log($"ListCampaigns error: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }

        public static object GoToLevel(int campaignIndex, int levelIndex)
        {
            try
            {
                // Setup player 1 if they haven't joined yet (e.g., from main menu)
                if (!HeroController.IsPlayerPlaying(0))
                {
                    PlayerProgress.currentWorldMapSaveSlot = 0;
                    GameState.Instance.currentWorldmapSave = PlayerProgress.Instance.saveSlots[0];
                    
                    HeroController.PIDS[0] = PID.MyID;
                    HeroController.playerControllerIDs[0] = 0;
                    HeroController.SetPlayerName(0, PlayerOptions.Instance.playerName);
                    HeroController.SetIsPlaying(0, true);
                }
                
                LevelSelectionController.ResetLevelAndGameModeToDefault();
                GameState.Instance.ResetToDefault();
                
                // Campaign names mapping (from Utility Mod)
                string[] campaignNames = new string[]
                {
                    "WM_Intro(mouse)",
                    "WM_Mission1(mouse)",
                    "WM_Mission2 (mouse)",
                    "WM_City1(mouse)",
                    "WM_Bombardment(mouse)",
                    "WM_Village1(mouse)",
                    "WM_City2(mouse)",
                    "WM_Bombardment2(mouse)",
                    "WM_KazakhstanIndustrial(mouse)",
                    "WM_KazakhstanRainy(mouse)",
                    "WM_AlienMission1(mouse)",
                    "WM_AlienMission2(mouse)",
                    "WM_AlienMission3(mouse)",
                    "WM_AlienMission4(mouse)",
                    "WM_HELL",
                    "WM_Whitehouse",
                    "Challenge_Alien",
                    "Challenge_Bombardment1",
                    "Challenge_Ammo",
                    "Challenge_Dash",
                    "Challenge_Mech",
                    "Challenge_MacBrover",
                    "Challenge_TimeBro",
                    "MuscleTemple_5Stages1",
                    "MuscleTemple_5Stages2",
                    "MuscleTemple_5Stages3",
                    "MuscleTemple_5Stages4",
                    "MuscleTemple_5Stages5"
                };
                
                if (campaignIndex >= 0 && campaignIndex < campaignNames.Length)
                {
                    GameState.Instance.campaignName = campaignNames[campaignIndex];
                    LevelSelectionController.CurrentLevelNum = levelIndex;
                }
                else
                {
                    // Use fallback like Utility Mod does
                    GameState.Instance.campaignName = campaignNames[0];
                    LevelSelectionController.CurrentLevelNum = 0;
                }
                
                GameState.Instance.loadMode = MapLoadMode.Campaign;
                GameState.Instance.gameMode = GameMode.Campaign;
                GameState.Instance.returnToWorldMap = true;
                GameState.Instance.sceneToLoad = LevelSelectionController.CampaignScene;
                GameState.Instance.sessionID = Connect.GetIncrementedSessionID().AsByte;
                
                // Check if we're on the main thread
                bool isMainThread = System.Threading.Thread.CurrentThread.ManagedThreadId == 1;
                
                if (isMainThread)
                {
                    // Already on main thread, call directly
                    GameModeController.LoadNextScene(GameState.Instance);
                }
                else
                {
                    // Queue to main thread
                    try
                    {
                        MainThreadDispatcher.EnqueueAndWait(() =>
                        {
                            GameModeController.LoadNextScene(GameState.Instance);
                        }, 3000);
                    }
                    catch (Exception ex)
                    {
                        Main.Log($"[GoToLevel] Failed to execute on main thread: {ex.Message}");
                        throw;
                    }
                }
                
                var result = new
                {
                    success = true,
                    message = $"Loading {campaignNames[campaignIndex]} - Level {levelIndex + 1}"
                };
                
                return result;
            }
            catch (Exception ex)
            {
                Main.Log($"GoToLevel error: {ex.Message}");
                return new
                {
                    success = false,
                    error = ex.Message
                };
            }
        }
    }
}