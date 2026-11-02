using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RocketLib.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity_Inspector_Mod
{
    public class MessageHandler
    {
        private readonly Dictionary<string, Func<JObject, object>> handlers;

        // Handlers that need to know which connection sent the request.
        private readonly Dictionary<string, Func<JObject, object, object>> clientHandlers;

        public MessageHandler()
        {
            handlers = new Dictionary<string, Func<JObject, object>>
            {
                ["ping"] = HandlePing,
                ["echo"] = HandleEcho,
                ["list_gameobjects"] = HandleListGameObjects,
                ["inspect_gameobject"] = HandleInspectGameObject,
                ["inspect_component"] = HandleInspectComponent,
                ["query_gameobjects"] = HandleQueryGameObjects,
                ["inspect_player"] = HandleInspectPlayer,
                ["list_enemies"] = HandleListEnemies,
                ["take_screenshot"] = HandleScreenshot,
                ["execute_code"] = HandleExecuteCode,
                ["modify_component"] = HandleModifyComponent,
                ["teleport_player"] = HandleTeleportPlayer,
                ["set_player_health"] = HandleSetPlayerHealth,
                ["spawn_entity"] = HandleSpawnEntity,
                ["set_game_speed"] = HandleSetGameSpeed,
                ["go_to_level"] = HandleGoToLevel,
                ["list_campaigns"] = HandleListCampaigns,
                ["simulate_input"] = HandleSimulateInput,
                ["game_state"] = HandleGameState,
                ["execute_script"] = HandleExecuteScript,
                ["compile_script"] = HandleCompileScript,
                ["unload_script"] = HandleUnloadScript,
                ["list_active_scripts"] = HandleListActiveScripts,
                ["swap_bro"] = HandleSwapBro,
                ["set_bro"] = HandleSetBro,
                ["list_bros"] = HandleListBros,
                ["restart_level"] = HandleRestartLevel
            };

            clientHandlers = new Dictionary<string, Func<JObject, object, object>>
            {
                ["trace_spawns"] = HandleTraceSpawns
            };
        }

        public string HandleMessage( string message, object client )
        {
            try
            {
                var request = JObject.Parse( message );
                var id = request["id"]?.ToString();
                var method = request["method"]?.ToString();
                var parameters = request["params"] as JObject;

                if ( string.IsNullOrEmpty( method ) )
                {
                    return CreateErrorResponse( id, "Missing method" );
                }

                if ( handlers.ContainsKey( method ) )
                {
                    var result = handlers[method]( parameters );
                    return CreateSuccessResponse( id, result );
                }
                else if ( clientHandlers.ContainsKey( method ) )
                {
                    var result = clientHandlers[method]( parameters, client );
                    return CreateSuccessResponse( id, result );
                }
                else
                {
                    return CreateErrorResponse( id, $"Unknown method: {method}" );
                }
            }
            catch ( JsonException ex )
            {
                return CreateErrorResponse( null, $"Invalid JSON: {ex.Message}" );
            }
            catch ( Exception ex )
            {
                Main.Log( $"Error handling message: {ex}" );
                return CreateErrorResponse( null, $"Internal error: {ex.Message}" );
            }
        }

        private string CreateSuccessResponse( string id, object result )
        {
            var response = new JObject
            {
                ["id"] = id,
                ["success"] = true,
                ["result"] = result != null ? JToken.FromObject( result ) : null
            };
            return response.ToString( Formatting.None );
        }

        private string CreateErrorResponse( string id, string error )
        {
            var response = new JObject
            {
                ["id"] = id,
                ["success"] = false,
                ["error"] = error
            };
            return response.ToString( Formatting.None );
        }

        private object HandlePing( JObject parameters )
        {
            return new { message = "pong", timestamp = DateTime.UtcNow };
        }

        private object HandleEcho( JObject parameters )
        {
            var message = parameters?["message"]?.ToString() ?? "No message provided";
            return new { echo = message };
        }

        private object HandleListGameObjects( JObject parameters )
        {
            var includeInactive = parameters?["includeInactive"]?.Value<bool>() ?? true;
            var maxResults = parameters?["maxResults"]?.Value<int>() ?? 100;
            
            
            var rootObjects = GameObjectInspector.GetRootGameObjects( includeInactive );

            var limitedObjects = rootObjects.Take(maxResults).ToList();

            return new
            {
                totalCount = rootObjects.Count,
                returnedCount = limitedObjects.Count,
                truncated = rootObjects.Count > maxResults,
                gameObjects = limitedObjects.Select( go => new
                {
                    name = go.name,
                    path = GetGameObjectPath( go ),
                    isActive = go.activeSelf,
                    childCount = go.transform.childCount
                } ).ToArray()
            };
        }

        private object HandleInspectGameObject( JObject parameters )
        {
            
            var path = parameters?["path"]?.ToString();
            if ( string.IsNullOrEmpty( path ) )
            {
                throw new ArgumentException( "GameObject path is required" );
            }

            var detailed = parameters?["detailed"]?.ToObject<bool>() ?? false;

            GameObject go = FindGameObjectByPath( path );

            if ( go == null )
            {
                throw new ArgumentException( $"GameObject not found: {path}" );
            }

            return GameObjectInspector.InspectGameObject( go, detailed );
        }

        private object HandleInspectComponent( JObject parameters )
        {
            var path = parameters?["path"]?.ToString();
            var componentType = parameters?["componentType"]?.ToString();
            var memberPath = parameters?["memberPath"]?.ToString();
            
            if ( string.IsNullOrEmpty( path ) )
            {
                throw new ArgumentException( "GameObject path is required" );
            }
            
            if ( string.IsNullOrEmpty( componentType ) )
            {
                throw new ArgumentException( "Component type is required" );
            }

            GameObject go = FindGameObjectByPath( path );

            if ( go == null )
            {
                throw new ArgumentException( $"GameObject not found: {path}" );
            }

            if ( string.IsNullOrEmpty( memberPath ) )
            {
                return GameObjectInspector.InspectSpecificComponent( go, componentType, null );
            }

            // A dotted path reads a struct link and its leaf out of one object graph; the main
            // thread is where the game leaves that graph consistent.
            object result = null;

            MainThreadDispatcher.EnqueueAndWait( () =>
            {
                result = GameObjectInspector.InspectSpecificComponent( go, componentType, memberPath );
            }, 5000 );

            return result;
        }

        private object HandleQueryGameObjects( JObject parameters )
        {
            var namePattern = parameters?["namePattern"]?.ToString();
            var componentType = parameters?["componentType"]?.ToString();
            var includeInactive = parameters?["includeInactive"]?.Value<bool>() ?? true;
            var maxResults = parameters?["maxResults"]?.Value<int>() ?? 100;

            return GameObjectInspector.QueryGameObjects( namePattern, componentType, includeInactive, maxResults );
        }

        private object HandleInspectPlayer( JObject parameters )
        {
            try
            {
                
                var players = HeroController.players;
                if ( players == null || players.Length == 0 )
                {
                    return new { players = new object[0] };
                }


                var results = new List<object>();
                for ( int i = 0; i < players.Length; i++ )
                {
                    try
                    {
                        var player = players[i];
                        if ( player != null && player.character != null )
                        {
                            var go = player.character.gameObject;
                            
                            results.Add( new
                            {
                                playerIndex = i,
                                playerNum = player.playerNum,
                                name = go.name,
                                position = new { x = go.transform.position.x, y = go.transform.position.y },
                                isActive = go.activeSelf
                            } );
                        }
                    }
                    catch ( Exception ex )
                    {
                        Main.Log( $"HandleInspectPlayer - Error processing player {i}: {ex.Message}" );
                    }
                }

                return new { players = results };
            }
            catch ( Exception ex )
            {
                Main.Log( $"HandleInspectPlayer - Fatal error: {ex.Message}\n{ex.StackTrace}" );
                return new { error = ex.Message, players = new object[0] };
            }
        }

        private object HandleListEnemies( JObject parameters )
        {
            try
            {
                object result = null;

                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    result = ListEnemiesOnMainThread();
                }, 5000 );

                return result;
            }
            catch ( Exception ex )
            {
                Main.Log( $"HandleListEnemies - Fatal error: {ex.Message}\n{ex.StackTrace}" );
                return new { error = ex.Message, count = 0, mapLoaded = false, enemies = new object[0] };
            }
        }

        private static object ListEnemiesOnMainThread()
        {
            var registry = Map.units;
            bool mapLoaded = Map.Instance != null && registry != null;

            var enemies = new List<object>();
            var seen = new HashSet<int>();

            if ( registry != null )
            {
                for ( int i = 0; i < registry.Count; i++ )
                {
                    AddEnemy( registry[i], seen, enemies );
                }
            }

            if ( mapLoaded )
            {
                // Boss derives from NetworkedUnit rather than Unit, and sandworms never call RegisterUnit.
                foreach ( var unit in UnityEngine.Object.FindObjectsOfType<NetworkedUnit>() )
                {
                    AddEnemy( unit, seen, enemies );
                }
            }

            if ( enemies.Count > 0 )
            {
                return new { count = enemies.Count, mapLoaded, enemies = enemies.ToArray() };
            }

            string[] suppressedBy = mapLoaded ? StateModifier.GetActiveEnemySpawnSuppressors() : new string[0];
            string[] enemySpawnPrefixOwners = mapLoaded ? StateModifier.GetEnemySpawnPrefixOwners() : new string[0];
            return new
            {
                count = 0,
                mapLoaded,
                enemies = new object[0],
                enemySpawnSuppressed = suppressedBy.Length > 0,
                suppressedBy,
                enemySpawnPrefixOwners
            };
        }

        // RocketLib's group arrays are disjoint today; first group named wins if that ever stops holding.
        private static readonly Dictionary<UnitType, string> unitCategories = BuildUnitCategories();

        private static Dictionary<UnitType, string> BuildUnitCategories()
        {
            var categories = new Dictionary<UnitType, string>();
            AddUnitCategory( categories, UnitTypes.NormalUnits, "normal" );
            AddUnitCategory( categories, UnitTypes.AlienUnits, "alien" );
            AddUnitCategory( categories, UnitTypes.HellUnits, "hell" );
            AddUnitCategory( categories, UnitTypes.BossUnits, "boss" );
            AddUnitCategory( categories, UnitTypes.WormUnits, "worm" );
            AddUnitCategory( categories, UnitTypes.VehicleEnemies, "vehicle" );
            AddUnitCategory( categories, UnitTypes.FriendlyUnits, "friendly" );
            AddUnitCategory( categories, UnitTypes.OtherUnits, "other" );
            categories[UnitType.None] = "none";
            return categories;
        }

        private static void AddUnitCategory( Dictionary<UnitType, string> categories, UnitType[] types, string category )
        {
            for ( int i = 0; i < types.Length; i++ )
            {
                if ( !categories.ContainsKey( types[i] ) )
                {
                    categories[types[i]] = category;
                }
            }
        }

        private static void AddEnemy( NetworkedUnit unit, HashSet<int> seen, List<object> enemies )
        {
            try
            {
                if ( unit == null || unit.gameObject == null ) return;
                // Map.units holds units whose GameObject is inactive - 52 pre-spawned face huggers on Challenge_Alien.
                if ( !unit.gameObject.activeInHierarchy ) return;
                if ( !unit.IsEnemy ) return;
                if ( !seen.Add( unit.GetInstanceID() ) ) return;

                // RocketLib classifies Unit and TestVanDammeAnim; a NetworkedUnit that is neither has no classification.
                Unit classifiable = unit as Unit;
                string unitType = null;
                string category = null;
                if ( classifiable != null )
                {
                    UnitType resolved = classifiable.GetUnitType();
                    unitType = resolved.ToString();
                    string named;
                    category = unitCategories.TryGetValue( resolved, out named ) ? named : "unknown";
                }

                enemies.Add( new
                {
                    name = unit.name,
                    type = unit.GetType().Name,
                    unitType,
                    category,
                    isMook = unit.IsMook,
                    position = new { x = unit.X, y = unit.Y },
                    health = unit.health,
                    isAlive = unit.health > 0
                } );
            }
            catch ( Exception ex )
            {
                Main.Log( $"HandleListEnemies - Error processing unit: {ex.Message}" );
            }
        }

        private object HandleScreenshot( JObject parameters )
        {
            var capture = ScreenshotCapture.TakeScreenshot( parameters?["highlight"] );

            return new
            {
                success = true,
                path = capture.Path,
                highlighted = capture.Highlighted,
                highlightError = capture.HighlightError,
                timestamp = DateTime.UtcNow
            };
        }

        private object HandleExecuteCode( JObject parameters )
        {
            var code = parameters?["code"]?.ToString();
            if ( string.IsNullOrEmpty( code ) )
            {
                throw new ArgumentException( "Code is required" );
            }

            
            try
            {
                var result = CodeExecutor.Execute(code);
                return result;
            }
            catch (Exception ex)
            {
                Main.Log($"Code execution failed: {ex.Message}");
                return new 
                { 
                    success = false, 
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                };
            }
        }

        // The path resolution inspect_gameobject and inspect_component take: GameObject.Find first,
        // then a scan by built path, since GameObject.Find only searches the active scene hierarchy.
        public static GameObject FindGameObjectByPath( string path )
        {
            GameObject go = GameObject.Find( path );

            if ( go == null )
            {
                var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                foreach ( var transform in allTransforms )
                {
                    if ( transform.gameObject != null && GetGameObjectPath( transform.gameObject ) == path )
                    {
                        go = transform.gameObject;
                        break;
                    }
                }
            }

            return go;
        }

        private static string GetGameObjectPath( GameObject go )
        {
            var path = go.name;
            var parent = go.transform.parent;
            while ( parent != null )
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return "/" + path;
        }

        private object HandleModifyComponent(JObject parameters)
        {
            var path = parameters?["path"]?.ToString();
            var componentType = parameters?["component"]?.ToString();
            var properties = parameters?["properties"] as JObject;

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(componentType) || properties == null)
            {
                throw new ArgumentException("Path, component type, and properties are required");
            }

            return StateModifier.ModifyComponent(path, componentType, properties);
        }

        private object HandleTeleportPlayer(JObject parameters)
        {
            var x = parameters?["x"]?.Value<float>();
            var y = parameters?["y"]?.Value<float>();
            var z = parameters?["z"]?.Value<float>();

            if (!x.HasValue || !y.HasValue)
            {
                throw new ArgumentException("X and Y coordinates are required");
            }

            var playerNum = parameters?["playerNum"]?.Value<int>() ?? 0;
            return StateModifier.TeleportPlayer(x.Value, y.Value, z, playerNum);
        }

        private object HandleSetPlayerHealth(JObject parameters)
        {
            var health = parameters?["health"]?.Value<int>();

            if (!health.HasValue)
            {
                throw new ArgumentException("Health value is required");
            }

            var playerNum = parameters?["playerNum"]?.Value<int>() ?? 0;
            return StateModifier.SetPlayerHealth(health.Value, playerNum);
        }

        private object HandleSpawnEntity(JObject parameters)
        {
            var entityType = parameters?["type"]?.ToString();
            var x = parameters?["x"]?.Value<float>();
            var y = parameters?["y"]?.Value<float>();
            var xI = parameters?["xI"]?.Value<float>();
            var yI = parameters?["yI"]?.Value<float>();

            if (string.IsNullOrEmpty(entityType) || !x.HasValue || !y.HasValue)
            {
                throw new ArgumentException("Entity type and coordinates are required");
            }

            var ignoreSuppression = parameters?["ignoreSuppression"]?.ToObject<bool?>() ?? false;
            return StateModifier.SpawnEntity(entityType, x.Value, y.Value, xI ?? 0f, yI ?? 0f, ignoreSuppression);
        }

        private object HandleSetGameSpeed(JObject parameters)
        {
            var speed = parameters?["speed"]?.Value<float>();

            if (!speed.HasValue)
            {
                throw new ArgumentException("Speed value is required");
            }

            return StateModifier.SetGameSpeed(speed.Value);
        }
        
        private object HandleGoToLevel(JObject parameters)
        {
            var campaignIndex = parameters?["campaignIndex"]?.Value<int>();
            var levelIndex = parameters?["levelIndex"]?.Value<int>();

            if (!campaignIndex.HasValue || !levelIndex.HasValue)
            {
                throw new ArgumentException("Campaign index and level index are required");
            }

            return StateModifier.GoToLevel(campaignIndex.Value, levelIndex.Value);
        }

        private object HandleListCampaigns(JObject parameters)
        {
            return StateModifier.ListCampaigns();
        }
        
        private object HandleSimulateInput( JObject parameters )
        {
            var action = parameters?["action"]?.ToString();

            List<InputSimulator.InputStep> sequence = null;
            var rawSequence = parameters?["sequence"];
            var steps = rawSequence as JArray;

            if ( rawSequence != null && rawSequence.Type != JTokenType.Null && steps == null )
            {
                return new { success = false, error = "sequence must be an array of steps" };
            }

            if ( steps != null && steps.Count > 0 )
            {
                sequence = new List<InputSimulator.InputStep>();

                for ( int i = 0; i < steps.Count; i++ )
                {
                    var step = steps[i] as JObject;
                    if ( step == null )
                    {
                        return new { success = false, error = $"Step {i} is not an object" };
                    }

                    sequence.Add( new InputSimulator.InputStep
                    {
                        Action = step["action"]?.ToString(),
                        Duration = step["duration"]?.ToObject<int?>() ?? InputSimulator.DefaultDurationMs,
                        Wait = step["wait"]?.ToObject<int?>() ?? 0
                    } );
                }
            }
            else if ( string.IsNullOrEmpty( action ) )
            {
                throw new ArgumentException( "Input action is required" );
            }

            var duration = parameters?["duration"]?.ToObject<int?>();
            var playerNum = parameters?["player"]?.ToObject<int?>();
            var count = parameters?["count"]?.ToObject<int?>();
            var interval = parameters?["interval"]?.ToObject<int?>();

            return InputSimulator.SimulateInput( action, duration, playerNum, count, interval, sequence );
        }

        private object HandleExecuteScript(JObject parameters)
        {
            var source = parameters?["source"]?.ToString();
            var name = parameters?["name"]?.ToString();

            if (string.IsNullOrEmpty(source))
                throw new ArgumentException("Script source is required");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Script name is required");

            Dictionary<string, string> args = null;
            var argsToken = parameters?["args"] as JObject;
            if (argsToken != null)
            {
                args = new Dictionary<string, string>();
                foreach (var prop in argsToken.Properties())
                {
                    args[prop.Name] = prop.Value?.ToString() ?? "";
                }
            }

            return ScriptManager.ExecuteScript(name, source, args);
        }

        private object HandleCompileScript(JObject parameters)
        {
            var source = parameters?["source"]?.ToString();
            var name = parameters?["name"]?.ToString();

            if (string.IsNullOrEmpty(source))
                throw new ArgumentException("Script source is required");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Script name is required");

            return ScriptManager.CompileOnly(name, source);
        }

        private object HandleUnloadScript(JObject parameters)
        {
            var name = parameters?["name"]?.ToString();

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Script name is required");

            return ScriptManager.UnloadScript(name);
        }

        private object HandleListActiveScripts(JObject parameters)
        {
            return ScriptManager.ListActiveScripts();
        }

        private object HandleGameState( JObject parameters )
        {
            try
            {
                var scene = SceneManager.GetActiveScene();

                string gameMode = null;
                try
                {
                    gameMode = GameModeController.GameMode.ToString();
                }
                catch { }

                int? levelIndex = null;
                int? levelNumber = null;
                string campaignName = null;
                bool isInGame = false;
                try
                {
                    levelIndex = LevelSelectionController.CurrentLevelNum;
                    levelNumber = levelIndex + 1;
                    var currentCampaign = LevelSelectionController.currentCampaign;
                    if ( currentCampaign != null )
                    {
                        campaignName = StateModifier.GetCanonicalCampaignName( currentCampaign.name );
                    }
                }
                catch { }

                string campaignDisplay = null;
                try
                {
                    campaignDisplay = StateModifier.GetCampaignDisplayName( campaignName );
                }
                catch { }

                bool isCampaignScene = false;
                try
                {
                    isCampaignScene = LevelSelectionController.IsCampaignScene;
                }
                catch { }

                string levelName = null;
                try
                {
                    if ( Map.MapData != null && !string.IsNullOrEmpty( Map.MapData.levelDescription ) )
                    {
                        levelName = Map.MapData.levelDescription;
                    }
                }
                catch { }

                bool levelFinished = false;
                string pauseState = null;
                try
                {
                    isInGame = LevelSelectionController.CurrentlyInGame
                        && GameModeController.Instance != null;
                    levelFinished = GameModeController.IsLevelFinished();
                }
                catch { }

                try
                {
                    pauseState = PauseController.pauseStatus.ToString();
                }
                catch { }

                var players = new List<object>();
                try
                {
                    if ( HeroController.players != null )
                    {
                        for ( int i = 0; i < HeroController.players.Length; i++ )
                        {
                            var player = HeroController.players[i];
                            if ( player == null ) continue;
                            if ( player.character != null )
                            {
                                var character = player.character;
                                players.Add( new
                                {
                                    playerIndex = i,
                                    playerNum = player.playerNum,
                                    broName = character.gameObject.name,
                                    heroType = character.heroType.ToString(),
                                    className = character.GetType().Name,
                                    position = new { x = character.X, y = character.Y },
                                    health = character.health,
                                    isAlive = character.health > 0,
                                    lives = player.Lives
                                } );
                            }
                            else if ( HeroController.IsPlaying( i ) )
                            {
                                players.Add( new
                                {
                                    playerIndex = i,
                                    playerNum = player.playerNum,
                                    broName = (string)null,
                                    heroType = player.heroType.ToString(),
                                    className = (string)null,
                                    position = (object)null,
                                    health = 0,
                                    isAlive = false,
                                    lives = player.Lives
                                } );
                            }
                        }
                    }
                }
                catch ( Exception ex )
                {
                    Main.Log( $"HandleGameState - Error getting player info: {ex.Message}" );
                }

                float timeScale = Time.timeScale;

                return new
                {
                    scene = scene.name,
                    isCampaignScene,
                    gameMode,
                    isInGame,
                    levelFinished,
                    pauseState,
                    campaignDisplay,
                    campaignName,
                    levelIndex,
                    levelNumber,
                    levelName,
                    timeScale,
                    playerCount = players.Count,
                    players
                };
            }
            catch ( Exception ex )
            {
                Main.Log( $"HandleGameState - Fatal error: {ex.Message}\n{ex.StackTrace}" );
                return new { error = ex.Message };
            }
        }

        private object HandleSwapBro( JObject parameters )
        {
            var broName = parameters?["broName"]?.ToString();
            if ( string.IsNullOrEmpty( broName ) )
                throw new ArgumentException( "Bro name is required" );

            var playerNum = parameters?["playerNum"]?.Value<int>() ?? 0;
            var ignoreUnlocks = parameters?["ignoreUnlocks"]?.ToObject<bool?>() ?? false;
            return StateModifier.SwapBro( broName, playerNum, ignoreUnlocks );
        }

        private object HandleSetBro( JObject parameters )
        {
            var broName = parameters?["broName"]?.ToString();
            if ( string.IsNullOrEmpty( broName ) )
                throw new ArgumentException( "Bro name is required" );

            var playerNum = parameters?["playerNum"]?.Value<int>() ?? 0;
            return StateModifier.SetBro( broName, playerNum );
        }

        private object HandleListBros( JObject parameters )
        {
            var playerNum = parameters?["playerNum"]?.Value<int>() ?? 0;
            return StateModifier.ListBros( playerNum );
        }

        private object HandleRestartLevel( JObject parameters )
        {
            return StateModifier.RestartLevel();
        }

        private object HandleTraceSpawns( JObject parameters, object client )
        {
            var action = parameters?["action"]?.ToString();

            try
            {
                object result = null;

                switch ( action )
                {
                    case "start":
                        string[] targets = null;
                        var rawTargets = parameters?["targets"];
                        if ( rawTargets != null && rawTargets.Type != JTokenType.Null )
                        {
                            var array = rawTargets as JArray;
                            if ( array == null )
                            {
                                return new { success = false, error = "targets must be an array of target names" };
                            }
                            targets = array.Select( t => t.ToString() ).ToArray();
                        }

                        MainThreadDispatcher.EnqueueAndWait( () => { result = SpawnTrace.Start( targets, client ); }, 5000 );
                        return result;

                    case "read":
                        long since = parameters?["since"]?.ToObject<long?>() ?? 0;
                        MainThreadDispatcher.EnqueueAndWait( () => { result = SpawnTrace.Read( since ); }, 5000 );
                        return result;

                    case "stop":
                        MainThreadDispatcher.EnqueueAndWait( () => { result = SpawnTrace.Stop(); }, 5000 );
                        return result;

                    default:
                        return new { success = false, error = "action must be 'start', 'read' or 'stop'" };
                }
            }
            catch ( Exception ex )
            {
                Main.Log( $"HandleTraceSpawns error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }
    }
}