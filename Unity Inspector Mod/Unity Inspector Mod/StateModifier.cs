using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
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
                
                FieldInfo field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    object convertedValue = ConvertValue(value, field.FieldType);
                    field.SetValue(target, convertedValue);
                    return true;
                }

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

            return value.ToObject(targetType);
        }

        private static GameObject FindGameObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (path == "/")
                return null;

            string[] parts = path.TrimStart('/').Split('/');
            
            GameObject[] rootObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            GameObject current = null;

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
        public static object TeleportPlayer(float x, float y, float? z = null, int playerNum = 0)
        {
            try
            {
                if (playerNum < -1 || playerNum > 3)
                {
                    return new { success = false, error = "Player number must be 0-3, or -1 for all players" };
                }

                var players = HeroController.players;

                if (playerNum == -1)
                {
                    if (players == null || players.Length == 0)
                    {
                        return new { success = false, error = "No players found" };
                    }

                    var results = new List<object>();
                    float finalZ = z ?? 0f;
                    for (int i = 0; i < players.Length; i++)
                    {
                        var p = players[i]?.character;
                        if (p == null) continue;
                        float pz = z ?? p.transform.position.z;
                        finalZ = pz;
                        p.transform.position = new Vector3(x, y, pz);
                        p.X = x;
                        p.Y = y;
                        results.Add(new { playerNum = i, playerName = p.name });
                    }

                    if (results.Count == 0)
                    {
                        return new { success = false, error = "No active players found" };
                    }

                    return new {
                        success = true,
                        newPosition = new { x = x, y = y, z = finalZ },
                        playerNum = -1,
                        results = results.ToArray()
                    };
                }

                if (players == null || players.Length <= playerNum || players[playerNum]?.character == null)
                {
                    return new { success = false, error = $"No player found at index {playerNum}" };
                }

                var player = players[playerNum].character;
                Vector3 newPosition = new Vector3(x, y, z ?? player.transform.position.z);
                player.transform.position = newPosition;

                player.X = x;
                player.Y = y;

                return new {
                    success = true,
                    newPosition = new { x = x, y = y, z = newPosition.z },
                    playerNum = playerNum,
                    playerName = player.name
                };
            }
            catch (Exception ex)
            {
                Main.Log($"TeleportPlayer error: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }

        public static object SetPlayerHealth(int health, int playerNum = 0)
        {
            try
            {
                if (playerNum < -1 || playerNum > 3)
                {
                    return new { success = false, error = "Player number must be 0-3, or -1 for all players" };
                }

                var players = HeroController.players;

                if (playerNum == -1)
                {
                    if (players == null || players.Length == 0)
                    {
                        return new { success = false, error = "No players found" };
                    }

                    var results = new List<object>();
                    for (int i = 0; i < players.Length; i++)
                    {
                        var p = players[i]?.character;
                        if (p == null) continue;
                        p.health = health;
                        if (health > 0 && p.actionState == ActionState.Dead)
                        {
                            p.actionState = ActionState.Idle;
                        }
                        results.Add(new { playerNum = i, playerName = p.name });
                    }

                    if (results.Count == 0)
                    {
                        return new { success = false, error = "No active players found" };
                    }

                    return new {
                        success = true,
                        health = health,
                        playerNum = -1,
                        results = results.ToArray()
                    };
                }

                if (players == null || players.Length <= playerNum || players[playerNum]?.character == null)
                {
                    return new { success = false, error = $"No player found at index {playerNum}" };
                }

                var player = players[playerNum].character;
                player.health = health;

                if (health > 0 && player.actionState == ActionState.Dead)
                {
                    player.actionState = ActionState.Idle;
                }

                return new {
                    success = true,
                    health = health,
                    playerNum = playerNum,
                    playerName = player.name
                };
            }
            catch (Exception ex)
            {
                Main.Log($"SetPlayerHealth error: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }

        public static object SpawnEntity(string entityType, float x, float y, float xI = 0f, float yI = 0f)
        {
            try
            {
                object result = null;

                MainThreadDispatcher.EnqueueAndWait(() =>
                {
                    result = SpawnEntityOnMainThread(entityType, x, y, xI, yI);
                }, 5000);

                return result;
            }
            catch (Exception ex)
            {
                Main.Log($"SpawnEntity error: {ex.Message}");
                return new { success = false, error = ex.Message, stackTrace = ex.StackTrace };
            }
        }

        private static object SpawnEntityOnMainThread(string entityType, float x, float y, float xI, float yI)
        {
            Map map = Map.Instance;
            ThemeHolder theme = map == null ? null : map.activeTheme;

            if (theme == null)
            {
                return new { success = false, error = "No level is loaded - a level must be loaded before entities can be spawned" };
            }

            var candidates = GetSpawnablePrefabs(theme);

            string requested = (entityType ?? string.Empty).Trim();
            if (string.Equals(requested, "facehugger", StringComparison.OrdinalIgnoreCase))
            {
                requested = "alienFaceHugger";
            }
            else if (string.Equals(requested, "baneling", StringComparison.OrdinalIgnoreCase))
            {
                requested = "alienBaneling";
            }

            string resolvedName = null;
            Mook prefab = null;

            foreach (var candidate in candidates)
            {
                if (MatchesFieldName(requested, candidate.Key))
                {
                    resolvedName = candidate.Key;
                    prefab = candidate.Value;
                    break;
                }
            }

            if (prefab == null)
            {
                foreach (var candidate in candidates)
                {
                    if (string.Equals(requested, candidate.Value.name, StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedName = candidate.Key;
                        prefab = candidate.Value;
                        break;
                    }
                }
            }

            if (prefab == null)
            {
                var availableTypes = new List<string>();
                foreach (var candidate in candidates)
                {
                    availableTypes.Add(candidate.Key);
                }

                return new
                {
                    success = false,
                    error = $"Unknown entity type '{entityType}'",
                    availableTypes = availableTypes.ToArray()
                };
            }

            TestVanDammeAnim spawned;
            bool networked;
            if (resolvedName == "alienFaceHugger")
            {
                spawned = Map.SpawnFaceHugger(x, y, xI, yI);
                networked = false;
            }
            else if (resolvedName == "alienBaneling")
            {
                spawned = Map.SpawnFaceBaneling(x, y, xI, yI);
                networked = false;
            }
            else
            {
                spawned = MapController.SpawnMook_Networked(prefab, x, y, xI, yI, false, false, false, false, true);
                networked = true;
            }

            if (spawned == null)
            {
                if (networked)
                {
                    return new
                    {
                        success = false,
                        error = $"Networked spawn of '{resolvedName}' returned no instance{DescribeSpawnMookPrefixes()}"
                    };
                }

                return new
                {
                    success = false,
                    error = $"Spawn helper for '{resolvedName}' returned no instance - an object may still have been spawned"
                };
            }

            return new
            {
                success = true,
                type = resolvedName,
                prefab = prefab.name,
                name = spawned.name,
                instanceId = spawned.gameObject.GetInstanceID(),
                networked = networked,
                x = x,
                y = y
            };
        }

        private static List<KeyValuePair<string, Mook>> GetSpawnablePrefabs(ThemeHolder theme)
        {
            var candidates = new List<KeyValuePair<string, Mook>>();

            FieldInfo[] fields = typeof(ThemeHolder).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (!typeof(TestVanDammeAnim).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                Mook prefab = field.GetValue(theme) as Mook;
                if (prefab != null)
                {
                    candidates.Add(new KeyValuePair<string, Mook>(field.Name, prefab));
                }
            }

            return candidates;
        }

        // A prefix that returns false leaves __result null, so the spawn silently produces nothing.
        private static string DescribeSpawnMookPrefixes()
        {
            try
            {
                MethodBase target = AccessTools.Method(typeof(MapController), "SpawnMook_Networked");
                if (target == null)
                {
                    return string.Empty;
                }

                Patches patches = Harmony.GetPatchInfo(target);
                if (patches == null || patches.Prefixes == null)
                {
                    return string.Empty;
                }

                var owners = new List<string>();
                foreach (var patch in patches.Prefixes)
                {
                    if (!string.IsNullOrEmpty(patch.owner) && !owners.Contains(patch.owner))
                    {
                        owners.Add(patch.owner);
                    }
                }

                if (owners.Count == 0)
                {
                    return string.Empty;
                }

                return $" - a Harmony prefix from {string.Join(", ", owners.ToArray())} may have suppressed the spawn (Utility Mod's disableEnemySpawn does this)";
            }
            catch (Exception ex)
            {
                Main.Log($"SpawnEntity prefix lookup error: {ex.Message}");
                return string.Empty;
            }
        }

        private static bool MatchesFieldName(string requested, string fieldName)
        {
            if (string.Equals(requested, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return fieldName.Length > 4 && fieldName.StartsWith("mook", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(requested, fieldName.Substring(4), StringComparison.OrdinalIgnoreCase);
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
                    GameState.Instance.campaignName = campaignNames[0];
                    LevelSelectionController.CurrentLevelNum = 0;
                }
                
                GameState.Instance.loadMode = MapLoadMode.Campaign;
                GameState.Instance.gameMode = GameMode.Campaign;
                GameState.Instance.returnToWorldMap = true;
                GameState.Instance.sceneToLoad = LevelSelectionController.CampaignScene;
                GameState.Instance.sessionID = Connect.GetIncrementedSessionID().AsByte;
                
                bool isMainThread = System.Threading.Thread.CurrentThread.ManagedThreadId == 1;
                
                if (isMainThread)
                {
                    GameModeController.LoadNextScene(GameState.Instance);
                }
                else
                {
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

        #region Bro Swapping

        private const string SwapBrosRequired = "Swap Bros Mod is required but not active";

        public static object SwapBro( string broName, int playerNum )
        {
            try
            {
                if ( playerNum < 0 || playerNum > 3 )
                {
                    return new { success = false, error = "Player number must be 0-3" };
                }

                if ( !SwapBrosIntegration.IsAvailable )
                {
                    return new { success = false, error = SwapBrosRequired };
                }

                object result = null;

                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    bool swapped = SwapBrosIntegration.SwapToBroByName( playerNum, broName );
                    if ( !swapped )
                    {
                        var available = SwapBrosIntegration.GetAvailableBros();
                        result = new
                        {
                            success = false,
                            error = $"Bro '{broName}' not found or swap failed",
                            availableBros = available.ToArray()
                        };
                        return;
                    }

                    var player = HeroController.players[playerNum];
                    result = new
                    {
                        success = true,
                        broName = player?.character?.gameObject?.name ?? broName,
                        heroType = player?.character?.heroType.ToString() ?? "Unknown"
                    };
                }, 5000 );

                return result;
            }
            catch ( Exception ex )
            {
                Main.Log( $"SwapBro error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }

        public static object SetBro( string broName, int playerNum )
        {
            try
            {
                if ( playerNum < 0 || playerNum > 3 )
                {
                    return new { success = false, error = "Player number must be 0-3" };
                }

                if ( !SwapBrosIntegration.IsAvailable )
                {
                    return new { success = false, error = SwapBrosRequired };
                }

                object result = null;

                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    bool set = SwapBrosIntegration.SetNextBroByName( playerNum, broName );
                    if ( !set )
                    {
                        var available = SwapBrosIntegration.GetAvailableBros();
                        result = new
                        {
                            success = false,
                            error = $"Bro '{broName}' not found",
                            availableBros = available.ToArray()
                        };
                        return;
                    }

                    result = new
                    {
                        success = true,
                        nextBro = broName
                    };
                }, 5000 );

                return result;
            }
            catch ( Exception ex )
            {
                Main.Log( $"SetBro error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }

        public static object ListBros( int playerNum = 0 )
        {
            try
            {
                if ( playerNum < 0 || playerNum > 3 )
                {
                    return new { success = false, error = "Player number must be 0-3" };
                }

                if ( !SwapBrosIntegration.IsAvailable )
                {
                    return new { success = false, error = SwapBrosRequired };
                }

                object result = null;

                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    var available = SwapBrosIntegration.GetAvailableBros();
                    var normal = SwapBrosIntegration.GetAllNormalBros();
                    var expendabros = SwapBrosIntegration.GetAllExpendabros();
                    var unfinished = SwapBrosIntegration.GetAllUnfinishedBros();

                    string currentBro = null;
                    try
                    {
                        currentBro = SwapBrosIntegration.GetCurrentBroName( playerNum );
                    }
                    catch { }

                    var currentBros = new List<object>();
                    var players = HeroController.players;
                    if ( players != null )
                    {
                        for ( int i = 0; i < players.Length; i++ )
                        {
                            if ( players[i]?.character == null ) continue;
                            string name = null;
                            try { name = SwapBrosIntegration.GetCurrentBroName( i ); }
                            catch { }
                            currentBros.Add( new { playerNum = i, currentBro = name } );
                        }
                    }

                    result = new
                    {
                        success = true,
                        playerNum,
                        currentBro,
                        currentBros = currentBros.ToArray(),
                        currentlyAvailable = available.ToArray(),
                        allNormal = normal.ToArray(),
                        allExpendabros = expendabros.ToArray(),
                        allUnfinished = unfinished.ToArray()
                    };
                }, 5000 );

                return result;
            }
            catch ( Exception ex )
            {
                Main.Log( $"ListBros error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }

        #endregion

        #region Level Control

        public static object RestartLevel()
        {
            try
            {
                object result = null;

                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    string sceneName = SceneManager.GetActiveScene().name;

                    Map.ClearSuperCheckpointStatus();

                    var field = typeof( TriggerManager ).GetField( "alreadyTriggeredTriggerOnceTriggers",
                        BindingFlags.Static | BindingFlags.NonPublic );
                    if ( field != null )
                    {
                        var triggers = field.GetValue( null ) as List<string>;
                        if ( triggers != null )
                        {
                            triggers.Clear();
                        }
                    }

                    PauseController.SetPause( PauseStatus.UnPaused );
                    GameModeController.RestartLevel();

                    result = new
                    {
                        success = true,
                        scene = sceneName
                    };
                }, 5000 );

                return result;
            }
            catch ( Exception ex )
            {
                Main.Log( $"RestartLevel error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }

        #endregion
    }
}