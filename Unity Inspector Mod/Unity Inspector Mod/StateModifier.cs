using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;
using RocketLib.Collections;
using RocketLib.Utils;

namespace Unity_Inspector_Mod
{
    public static class StateModifier
    {
        public static object ModifyComponent(string gameObjectPath, string componentType, JObject properties)
        {
            try
            {
                object result = null;

                // A struct link is read, written and stored back as one unit; off the main thread the
                // game can set a sibling field between the read and the store and lose it.
                MainThreadDispatcher.EnqueueAndWait(() =>
                {
                    result = ModifyComponentOnMainThread(gameObjectPath, componentType, properties);
                }, 5000);

                return result;
            }
            catch (Exception ex)
            {
                Main.Log($"ModifyComponent error: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }

        private static object ModifyComponentOnMainThread(string gameObjectPath, string componentType, JObject properties)
        {
            if (properties == null || properties.Count == 0)
            {
                return new { success = false, error = "No properties were given - pass at least one member name and value to write" };
            }

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
            var errors = new List<object>();

            foreach (var prop in properties)
            {
                string propName = prop.Key;
                JToken propValue = prop.Value;

                string reason;
                string[] suggestions;
                string failure = SetPropertyValue(component, propName, propValue, out reason, out suggestions);
                if (failure == null)
                {
                    modifiedProperties.Add(propName);
                }
                else if (suggestions != null)
                {
                    errors.Add(new { property = propName, reason, message = failure, suggestions });
                }
                else
                {
                    errors.Add(new { property = propName, reason, message = failure });
                }
            }

            return new
            {
                success = modifiedProperties.Count > 0 || errors.Count == 0,
                modifiedProperties = modifiedProperties.ToArray(),
                errors = errors.ToArray(),
                component = componentType,
                gameObject = gameObjectPath
            };
        }

        private static string SetPropertyValue(object target, string propertyName, JToken value, out string reason, out string[] suggestions)
        {
            reason = null;
            suggestions = null;

            if (!MemberPath.IsPath(propertyName))
            {
                return SetMemberValue(target, propertyName, propertyName, value, out reason, out suggestions);
            }

            try
            {
                MemberPathResolution resolution = MemberPath.ResolveOwner(target, propertyName);
                if (!resolution.Success)
                {
                    reason = resolution.Reason;
                    suggestions = resolution.Suggestions;
                    return resolution.Error;
                }

                // Refused before anything is written: a write past a boxed struct that cannot be
                // stored back would otherwise report success and change nothing.
                string blocked = MemberPath.CheckWriteBack(resolution, out reason);
                if (blocked != null)
                {
                    return blocked;
                }

                string failure = SetMemberValue(resolution.Owner, resolution.Leaf, propertyName, value, out reason, out suggestions);
                if (failure == null)
                {
                    MemberPath.WriteBack(resolution);
                }

                return failure;
            }
            catch (Exception ex)
            {
                // One property's failure, not the whole call's: the properties already written stay reported.
                Exception failure = MemberPath.Unwrap(ex);
                Main.Log($"SetPropertyValue error for {propertyName}: {failure.Message}");
                reason = "error";
                suggestions = null;
                return failure.Message;
            }
        }

        private static string SetMemberValue(object target, string memberName, string reportedName, JToken value, out string reason, out string[] suggestions)
        {
            const BindingFlags memberFlags = BindingFlags.DeclaredOnly | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            reason = null;
            suggestions = null;

            try
            {
                Type type = target.GetType();

                for (Type t = type; t != null; t = t.BaseType)
                {
                    FieldInfo field = t.GetField(memberName, memberFlags);
                    if (field != null)
                    {
                        if (field.IsLiteral)
                        {
                            reason = "read-only";
                            return $"Field {t.Name}.{field.Name} is a constant";
                        }

                        object convertedValue = ConvertValue(value, field.FieldType);
                        field.SetValue(field.IsStatic ? null : target, convertedValue);
                        return null;
                    }

                    PropertyInfo property = t.GetProperty(memberName, memberFlags);
                    if (property != null)
                    {
                        if (!property.CanWrite)
                        {
                            reason = "read-only";
                            return $"Property {t.Name}.{property.Name} has no setter";
                        }

                        MethodInfo setter = property.GetSetMethod(true);
                        object convertedValue = ConvertValue(value, property.PropertyType);
                        property.SetValue(setter != null && setter.IsStatic ? null : target, convertedValue, null);
                        return null;
                    }
                }

                reason = "not-found";
                suggestions = MemberPath.Suggest(type, memberName, MemberPath.PathPrefix(reportedName));
                return memberName == reportedName
                    ? $"No field or property named {memberName} on {type.Name} or any of its base types"
                    : $"No field or property named {memberName} on {type.Name} or any of its base types (resolving {reportedName})";
            }
            catch (Exception ex)
            {
                // A setter that threw arrives wrapped, and its own message is the one worth reporting.
                Exception failure = MemberPath.Unwrap(ex);
                Main.Log($"SetPropertyValue error for {reportedName}: {failure.Message}");
                reason = "error";
                suggestions = null;
                return failure.Message;
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
                if (playerNum < -1 || playerNum >= Nums.PLAYER_MAX)
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
                if (playerNum < -1 || playerNum >= Nums.PLAYER_MAX)
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

            if (map == null || map.activeTheme == null)
            {
                return new { success = false, error = "No level is loaded - a level must be loaded before entities can be spawned" };
            }

            UnitType unitType = ResolveUnitType(entityType);

            if (unitType == UnitType.None)
            {
                if (NamesBro(entityType))
                {
                    return new
                    {
                        success = false,
                        error = "'Bro' is a RocketLib unit type but cannot be spawned here - bros come from the character roster, so use set_bro or swap_bro to change the bro a player has"
                    };
                }

                return new
                {
                    success = false,
                    error = $"Unknown unit type '{entityType}' - names are RocketLib UnitType values, written either as the enum name (SuicideMook) or as the display name (Suicide Mook)",
                    availableTypes = UnitTypes.AllUnitNames
                };
            }

            string displayName = unitType.ToDisplayString();

            Unit prefab = null;
            string prefabFailure = null;

            // RocketLib reads the prefab out of the loaded theme and the level's shared assets, either of which can be absent.
            try
            {
                prefab = unitType.GetUnitPrefab();
            }
            catch (Exception ex)
            {
                prefabFailure = $"{ex.GetType().Name}: {ex.Message}";
            }

            if (prefab == null)
            {
                string reason = prefabFailure == null
                    ? "this level's theme and shared assets hold no prefab for it"
                    : $"reading its prefab from this level's theme and shared assets threw {prefabFailure}";

                return new
                {
                    success = false,
                    error = $"'{displayName}' cannot be spawned on this level - {reason}",
                    unitType = unitType.ToString()
                };
            }

            Unit spawned;
            bool networked;

            if (unitType == UnitType.Facehugger)
            {
                spawned = Map.SpawnFaceHugger(x, y, xI, yI);
                networked = false;
            }
            else if (unitType == UnitType.Screecher)
            {
                spawned = Map.SpawnFaceBaneling(x, y, xI, yI);
                networked = false;
            }
            else if (prefab is Mook)
            {
                spawned = MapController.SpawnMook_Networked(prefab as Mook, x, y, xI, yI, false, false, false, false, true);
                networked = true;
            }
            else
            {
                spawned = SpawnUnitLocal(prefab, x, y, xI, yI);
                networked = false;
            }

            if (spawned == null)
            {
                if (networked)
                {
                    return new
                    {
                        success = false,
                        error = $"Networked spawn of '{displayName}' returned no instance{DescribeSpawnMookPrefixes()}"
                    };
                }

                return new
                {
                    success = false,
                    error = $"Local spawn of '{displayName}' returned no instance - an object may still have been spawned"
                };
            }

            return new
            {
                success = true,
                type = displayName,
                unitType = unitType.ToString(),
                prefab = prefab.name,
                name = spawned.name,
                instanceId = spawned.gameObject.GetInstanceID(),
                networked = networked,
                x = x,
                y = y
            };
        }

        // Units register themselves with the Map in Awake/Start, so the spawn only has to place the instance and hand it to the deterministic registry.
        private static Unit SpawnUnitLocal(Unit prefab, float x, float y, float xI, float yI)
        {
            Unit unit = UnityEngine.Object.Instantiate<Unit>(prefab, new Vector3(x, y, 0f), Quaternion.identity);

            if (unit == null)
            {
                return null;
            }

            if (Map.Instance != null)
            {
                unit.transform.parent = Map.Instance.transform;
            }

            Registry.RegisterDeterminsiticGameObject(unit.gameObject);
            unit.xI = xI;
            unit.yI = yI;
            return unit;
        }

        private static UnitType ResolveUnitType(string requested)
        {
            string trimmed = (requested ?? string.Empty).Trim();

            if (trimmed.Length == 0)
            {
                return UnitType.None;
            }

            UnitType byDisplayName = UnitTypes.ToUnitType(trimmed);

            if (IsSpawnable(byDisplayName))
            {
                return byDisplayName;
            }

            string normalized = NormalizeUnitName(trimmed);

            foreach (UnitType candidate in Enum.GetValues(typeof(UnitType)))
            {
                if (!IsSpawnable(candidate))
                {
                    continue;
                }

                if (normalized == NormalizeUnitName(candidate.ToString()) ||
                    normalized == NormalizeUnitName(candidate.ToDisplayString()))
                {
                    return candidate;
                }
            }

            return UnitType.None;
        }

        // None names nothing, and Bro has no prefab of its own - bros come from the character roster.
        private static bool IsSpawnable(UnitType type)
        {
            return type != UnitType.None && type != UnitType.Bro;
        }

        // Bro resolves nowhere, so it would otherwise read as a name that does not exist.
        private static bool NamesBro(string requested)
        {
            string normalized = NormalizeUnitName((requested ?? string.Empty).Trim());

            return normalized.Length > 0 &&
                (normalized == NormalizeUnitName(UnitType.Bro.ToString()) ||
                 normalized == NormalizeUnitName(UnitType.Bro.ToDisplayString()));
        }

        private static string NormalizeUnitName(string name)
        {
            var normalized = new StringBuilder(name.Length);

            foreach (char character in name)
            {
                if (char.IsLetterOrDigit(character))
                {
                    normalized.Append(char.ToLowerInvariant(character));
                }
            }

            return normalized.ToString();
        }

        // A prefix that returns false leaves __result null, so the spawn silently produces nothing.
        private static string DescribeSpawnMookPrefixes()
        {
            string[] owners = GetPrefixOwners(new[]
            {
                new KeyValuePair<Type, string>(typeof(MapController), "SpawnMook_Networked")
            });

            if (owners.Length == 0)
            {
                return string.Empty;
            }

            return $" - a Harmony prefix from {string.Join(", ", owners)} may have suppressed the spawn (Utility Mod's disableEnemySpawn does this)";
        }

        public static string[] GetEnemySpawnPrefixOwners()
        {
            return GetPrefixOwners(new[]
            {
                new KeyValuePair<Type, string>(typeof(MapController), "SpawnMook_Networked"),
                new KeyValuePair<Type, string>(typeof(MapController), "SpawnMook_Local"),
                new KeyValuePair<Type, string>(typeof(Map), "PlaceDoodad")
            });
        }

        // Utility Mod registers its prefixes unconditionally and tests the setting inside them, so only the setting says whether spawning is actually suppressed.
        public static string[] GetActiveEnemySpawnSuppressors()
        {
            var suppressors = new List<string>();

            if (IsUtilityModSuppressingEnemySpawns())
            {
                suppressors.Add("Utility Mod");
            }

            return suppressors.ToArray();
        }

        private static bool IsUtilityModSuppressingEnemySpawns()
        {
            try
            {
                Type mainType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name != "Utility Mod")
                    {
                        continue;
                    }

                    mainType = assembly.GetType("Utility_Mod.Main");
                    break;
                }

                if (mainType == null)
                {
                    return false;
                }

                BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                FieldInfo enabledField = mainType.GetField("enabled", staticFlags);
                object enabled = enabledField == null ? null : enabledField.GetValue(null);
                if (!(enabled is bool) || !(bool)enabled)
                {
                    return false;
                }

                FieldInfo settingsField = mainType.GetField("settings", staticFlags);
                object settings = settingsField == null ? null : settingsField.GetValue(null);
                if (settings == null)
                {
                    Main.Log("Utility Mod is loaded but its settings field is missing or null - cannot tell whether enemy spawning is suppressed");
                    return false;
                }

                FieldInfo disableField = settings.GetType().GetField("disableEnemySpawn",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (disableField == null)
                {
                    Main.Log("Utility Mod is loaded but its disableEnemySpawn setting is missing - cannot tell whether enemy spawning is suppressed");
                    return false;
                }

                object disabled = disableField.GetValue(settings);
                return disabled is bool && (bool)disabled;
            }
            catch (Exception ex)
            {
                Main.Log($"Enemy spawn suppression check error: {ex.Message}");
                return false;
            }
        }

        private static string[] GetPrefixOwners(KeyValuePair<Type, string>[] targets)
        {
            var owners = new List<string>();

            foreach (var target in targets)
            {
                try
                {
                    MethodBase method = AccessTools.Method(target.Key, target.Value);
                    if (method == null)
                    {
                        continue;
                    }

                    Patches patches = Harmony.GetPatchInfo(method);
                    if (patches == null || patches.Prefixes == null)
                    {
                        continue;
                    }

                    foreach (var patch in patches.Prefixes)
                    {
                        if (!string.IsNullOrEmpty(patch.owner) && !owners.Contains(patch.owner))
                        {
                            owners.Add(patch.owner);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Main.Log($"Harmony prefix lookup error for {target.Value}: {ex.Message}");
                }
            }

            return owners.ToArray();
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
        
        private static readonly string[] CampaignNames = new string[]
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

        // The game stores the campaign name with runtime capitalisation ("WM_Hell"); the roster spelling is what go_to_level and list_campaigns use.
        public static string GetCanonicalCampaignName(string campaignName)
        {
            if (string.IsNullOrEmpty(campaignName))
            {
                return campaignName;
            }

            foreach (var known in CampaignNames)
            {
                if (string.Equals(known, campaignName, StringComparison.OrdinalIgnoreCase))
                {
                    return known;
                }
            }

            return campaignName;
        }

        public static string GetCampaignDisplayName(string campaignName)
        {
            if (string.IsNullOrEmpty(campaignName))
            {
                return null;
            }

            return FormatCampaignDisplayName(GetCanonicalCampaignName(campaignName));
        }

        private static string FormatCampaignDisplayName(string campaignName)
        {
            return campaignName.Replace("WM_", "").Replace("(mouse)", "").Replace("_", " ").Trim();
        }

        public static object ListCampaigns()
        {
            try
            {
                var campaigns = new List<object>();
                for (int i = 0; i < CampaignNames.Length; i++)
                {
                    campaigns.Add(new
                    {
                        index = i,
                        name = CampaignNames[i],
                        displayName = FormatCampaignDisplayName(CampaignNames[i])
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
                if (campaignIndex < 0 || campaignIndex >= CampaignNames.Length)
                {
                    return new
                    {
                        success = false,
                        error = $"Campaign index {campaignIndex} is out of range - valid indices are 0 to {CampaignNames.Length - 1} (see list_campaigns)"
                    };
                }

                if (levelIndex < 0)
                {
                    return new
                    {
                        success = false,
                        error = $"Level index {levelIndex} is out of range - level indices start at 0"
                    };
                }

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
                
                GameState.Instance.campaignName = CampaignNames[campaignIndex];
                LevelSelectionController.CurrentLevelNum = levelIndex;
                
                GameState.Instance.loadMode = MapLoadMode.Campaign;
                GameState.Instance.gameMode = GameMode.Campaign;
                GameState.Instance.returnToWorldMap = true;
                GameState.Instance.sceneToLoad = LevelSelectionController.CampaignScene;
                GameState.Instance.sessionID = Connect.GetIncrementedSessionID().AsByte;
                
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
                
                var result = new
                {
                    success = true,
                    message = $"Loading {CampaignNames[campaignIndex]} - Level {levelIndex + 1}"
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
                if ( playerNum < Nums.PLAYER_MIN || playerNum >= Nums.PLAYER_MAX )
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
                            availableBros = available.ToArray(),
                            allNormal = SwapBrosIntegration.GetAllNormalBros().ToArray(),
                            allExpendabros = SwapBrosIntegration.GetAllExpendabros().ToArray(),
                            allUnfinished = SwapBrosIntegration.GetAllUnfinishedBros().ToArray()
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
                if ( playerNum < Nums.PLAYER_MIN || playerNum >= Nums.PLAYER_MAX )
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
                            availableBros = available.ToArray(),
                            allNormal = SwapBrosIntegration.GetAllNormalBros().ToArray(),
                            allExpendabros = SwapBrosIntegration.GetAllExpendabros().ToArray(),
                            allUnfinished = SwapBrosIntegration.GetAllUnfinishedBros().ToArray()
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
                if ( playerNum < Nums.PLAYER_MIN || playerNum >= Nums.PLAYER_MAX )
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