using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RocketLib.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity_Inspector_Mod
{
    public class CallerErrorException : ArgumentException
    {
        public CallerErrorException( string message ) : base( message ) { }
    }

    public class MessageHandler
    {
        private readonly Dictionary<string, Func<JObject, object>> handlers;

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
                ["input_plan"] = InputSimulator.PlanStatus,
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
                ["trace_spawns"] = HandleTraceSpawns,
                ["simulate_input"] = InputSimulator.StartPlan,
                ["cancel_input_plan"] = InputSimulator.CancelPlan
            };
        }

        public string HandleMessage( string message, object client )
        {
            JObject request;
            try
            {
                request = ParseRequest( message );
            }
            catch ( JsonException ex )
            {
                return CreateErrorResponse( null, $"Invalid JSON: {ex.Message}" );
            }

            string id = null;
            try
            {
                id = request["id"]?.ToString();
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
            catch ( Exception ex ) when ( IsCallerError( ex ) )
            {
                return CreateErrorResponse( id, ex.Message );
            }
            catch ( Exception ex ) when ( MainThreadDispatcher.IsStall( ex ) )
            {
                return CreateSuccessResponse( id, new { success = false, error = ex.Message, mainThreadAnswered = false } );
            }
            catch ( Exception ex )
            {
                Main.Log( $"Error handling message: {ex}" );
                return CreateErrorResponse( id, $"Internal error: {ex.Message}" );
            }
        }

        private static bool IsCallerError( Exception ex )
        {
            if ( ex is CallerErrorException )
            {
                return true;
            }
            if ( !( ex is FormatException || ex is OverflowException || ex is JsonReaderException ) )
            {
                return false;
            }
            var frames = new System.Diagnostics.StackTrace( ex, false ).GetFrames();
            return frames != null && frames.Any( frame => frame.GetMethod()?.DeclaringType?.Namespace == "Newtonsoft.Json.Linq" );
        }

        internal static JObject ParseRequest( string message )
        {
            // JObject.Parse turns a string that looks like an ISO date into a DateTime, which ToString() prints in the game's culture.
            using ( var reader = new JsonTextReader( new StringReader( message ) ) { DateParseHandling = DateParseHandling.None } )
            {
                var request = JObject.Load( reader );
                // Read() throws on content trailing the object.
                while ( reader.Read() ) { }
                return request;
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

        internal sealed class ParamException : CallerErrorException
        {
            public ParamException( string message ) : base( message ) { }
        }

        private static JToken Given( JObject parameters, string key )
        {
            var token = parameters?[key];
            return token == null || token.Type == JTokenType.Null ? null : token;
        }

        private static string Sent( JToken token )
        {
            string text = token.ToString( Formatting.None );
            return text.Length > 60 ? text.Substring( 0, 60 ) + "..." : text;
        }

        private static double NumberValue( JToken token, string label )
        {
            if ( token.Type != JTokenType.Integer && token.Type != JTokenType.Float )
            {
                throw new ParamException( $"{label} must be a number, got {Sent( token )}" );
            }
            double value;
            try
            {
                value = Convert.ToDouble( ( (JValue)token ).Value, CultureInfo.InvariantCulture );
            }
            catch ( Exception )
            {
                throw new ParamException( $"{label} is out of range, got {Sent( token )}" );
            }
            if ( double.IsNaN( value ) || double.IsInfinity( value ) )
            {
                throw new ParamException( $"{label} must be a finite number, got {value.ToString( CultureInfo.InvariantCulture )}" );
            }
            return value;
        }

        internal static float? ReadFloat( JObject parameters, string key, string label = null )
        {
            var token = Given( parameters, key );
            if ( token == null )
            {
                return null;
            }
            float value = (float)NumberValue( token, label ?? key );
            if ( float.IsInfinity( value ) )
            {
                throw new ParamException( $"{label ?? key} is out of range, got {Sent( token )}" );
            }
            return value;
        }

        internal static long? ReadLong( JObject parameters, string key, long min, long max, string label = null )
        {
            var token = Given( parameters, key );
            if ( token == null )
            {
                return null;
            }
            label = label ?? key;
            object raw = token is JValue ? ( (JValue)token ).Value : null;
            long value;
            if ( raw is long || raw is int )
            {
                value = Convert.ToInt64( raw );
            }
            else
            {
                double number = NumberValue( token, label );
                if ( System.Math.Floor( number ) != number )
                {
                    throw new ParamException( $"{label} must be an integer, got {Sent( token )}" );
                }
                if ( number < min || number > max )
                {
                    throw new ParamException( $"{label} must be an integer from {min} to {max}, got {Sent( token )}" );
                }
                value = (long)number;
            }
            if ( value < min || value > max )
            {
                throw new ParamException( $"{label} must be an integer from {min} to {max}, got {Sent( token )}" );
            }
            return value;
        }

        internal static int? ReadInt( JObject parameters, string key, string label = null )
        {
            var value = ReadLong( parameters, key, int.MinValue, int.MaxValue, label );
            return value.HasValue ? (int?)value.Value : null;
        }

        internal static bool? ReadBool( JObject parameters, string key, string label = null )
        {
            var token = Given( parameters, key );
            if ( token == null )
            {
                return null;
            }
            if ( token.Type != JTokenType.Boolean )
            {
                throw new ParamException( $"{label ?? key} must be a boolean, got {Sent( token )}" );
            }
            return token.Value<bool>();
        }

        private const int PingMainThreadBudgetMs = 1000;
        private const int MainThreadBudgetMs = 5000;

        private object HandlePing( JObject parameters )
        {
            int? frame = null;
            try
            {
                MainThreadDispatcher.EnqueueAndWait( () => { frame = Time.frameCount; }, PingMainThreadBudgetMs );
            }
            catch ( Exception )
            {
            }

            return new { message = "pong", timestamp = DateTime.UtcNow, mainThreadAnswered = frame.HasValue, frame };
        }

        private static object OnMainThread( Func<object> read )
        {
            object result = null;
            MainThreadDispatcher.EnqueueAndWait( () => { result = read(); }, MainThreadBudgetMs );
            return result;
        }

        private object HandleEcho( JObject parameters )
        {
            var message = parameters?["message"]?.ToString() ?? "No message provided";
            return new { echo = message };
        }

        private object HandleListGameObjects( JObject parameters )
        {
            var includeInactive = ReadBool( parameters, "includeInactive" ) ?? true;
            var maxResults = ReadMaxResults( parameters );

            return OnMainThread( () => GameObjectInspector.ListGameObjects( includeInactive, maxResults ) );
        }

        private static int ReadMaxResults( JObject parameters )
        {
            int maxResults = ReadInt( parameters, "maxResults" ) ?? 100;
            if ( maxResults < 1 )
            {
                throw new CallerErrorException( $"maxResults must be at least 1, got {maxResults}" );
            }
            return maxResults;
        }

        private object HandleInspectGameObject( JObject parameters )
        {
            var path = parameters?["path"]?.ToString();
            var instanceId = ReadInt( parameters, "instanceId" );
            if ( string.IsNullOrEmpty( path ) && !instanceId.HasValue )
            {
                throw new CallerErrorException( "GameObject path or instanceId is required" );
            }

            var detailed = ReadBool( parameters, "detailed" ) ?? false;

            return OnMainThread( () =>
            {
                var target = GameObjectInspector.Resolve( path, instanceId );
                var reply = (Dictionary<string, object>)GameObjectInspector.InspectGameObject( target.GameObject, detailed );
                return GameObjectInspector.AddLocation( target, reply );
            } );
        }

        private object HandleInspectComponent( JObject parameters )
        {
            var path = parameters?["path"]?.ToString();
            var instanceId = ReadInt( parameters, "instanceId" );
            var componentType = parameters?["componentType"]?.ToString();
            var memberPath = parameters?["memberPath"]?.ToString();

            if ( string.IsNullOrEmpty( path ) && !instanceId.HasValue )
            {
                throw new CallerErrorException( "GameObject path or instanceId is required" );
            }

            if ( string.IsNullOrEmpty( componentType ) )
            {
                throw new CallerErrorException( "Component type is required" );
            }

            return OnMainThread( () =>
            {
                var target = GameObjectInspector.Resolve( path, instanceId );
                return GameObjectInspector.WithLocation( target, GameObjectInspector.InspectSpecificComponent( target.GameObject, componentType, memberPath ) );
            } );
        }

        private object HandleQueryGameObjects( JObject parameters )
        {
            var namePattern = parameters?["namePattern"]?.ToString();
            var componentType = parameters?["componentType"]?.ToString();
            var includeInactive = ReadBool( parameters, "includeInactive" ) ?? true;
            var maxResults = ReadMaxResults( parameters );

            return OnMainThread( () => GameObjectInspector.QueryGameObjects( namePattern, componentType, includeInactive, maxResults ) );
        }

        private object HandleInspectPlayer( JObject parameters )
        {
            Func<object> read = () =>
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
                        var slot = StateModifier.ReadSlot( i );
                        if ( slot.Character != null )
                        {
                            var go = slot.Character.gameObject;

                            results.Add( new
                            {
                                playerIndex = i,
                                playerNum = player.playerNum,
                                name = go.name,
                                position = new { x = go.transform.position.x, y = go.transform.position.y },
                                isActive = go.activeSelf,
                                activeInHierarchy = go.activeInHierarchy,
                                isAlive = slot.State == "alive",
                                state = slot.State,
                                piloting = slot.Piloting
                            } );
                        }
                        else if ( slot.State != "empty" )
                        {
                            results.Add( new
                            {
                                playerIndex = i,
                                playerNum = player != null ? player.playerNum : i,
                                name = (string)null,
                                position = (object)null,
                                isActive = false,
                                activeInHierarchy = false,
                                isAlive = false,
                                state = slot.State,
                                piloting = (string)null
                            } );
                        }
                    }
                    catch ( Exception ex )
                    {
                        Main.Log( $"HandleInspectPlayer - Error processing player {i}: {ex.Message}" );
                    }
                }

                return new { players = results };
            };

            try
            {
                return OnMainThread( read );
            }
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
            {
                Main.Log( $"HandleInspectPlayer - Fatal error: {ex.Message}\n{ex.StackTrace}" );
                return new { success = false, error = ex.Message, players = new object[0] };
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
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
            {
                Main.Log( $"HandleListEnemies - Fatal error: {ex.Message}\n{ex.StackTrace}" );
                return new { success = false, error = ex.Message };
            }
        }

        private static object ListEnemiesOnMainThread()
        {
            var registry = Map.units;
            bool mapLoaded = Map.Instance != null && registry != null;

            var enemies = new List<object>();
            var seen = new HashSet<int>();
            int aliveCount = 0;

            if ( registry != null )
            {
                for ( int i = 0; i < registry.Count; i++ )
                {
                    if ( AddEnemy( registry[i], seen, enemies ) )
                    {
                        aliveCount++;
                    }
                }
            }

            if ( mapLoaded )
            {
                // Boss derives from NetworkedUnit rather than Unit, and sandworms never call RegisterUnit.
                foreach ( var unit in UnityEngine.Object.FindObjectsOfType<NetworkedUnit>() )
                {
                    if ( AddEnemy( unit, seen, enemies ) )
                    {
                        aliveCount++;
                    }
                }
            }

            string[] suppressedBy = mapLoaded ? StateModifier.GetActiveEnemySpawnSuppressors() : new string[0];
            string[] enemySpawnPrefixOwners = mapLoaded ? StateModifier.GetEnemySpawnPrefixOwners() : new string[0];
            return new
            {
                count = enemies.Count,
                aliveCount,
                mapLoaded,
                enemies = enemies.ToArray(),
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

        private static bool AddEnemy( NetworkedUnit unit, HashSet<int> seen, List<object> enemies )
        {
            try
            {
                if ( unit == null || unit.gameObject == null )
                {
                    return false;
                }
                // Map.units holds units whose GameObject is inactive - 52 pre-spawned face huggers on Challenge_Alien.
                if ( !unit.gameObject.activeInHierarchy )
                {
                    return false;
                }
                // NetworkedUnit.IsEnemy caches its first answer, and piloting a vehicle changes IsHero afterwards.
                if ( unit.IsHero || unit is RescueBro )
                {
                    return false;
                }
                if ( !seen.Add( unit.GetInstanceID() ) )
                {
                    return false;
                }

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
                return unit.health > 0;
            }
            catch ( Exception ex )
            {
                Main.Log( $"HandleListEnemies - Error processing unit: {ex.Message}" );
                return false;
            }
        }

        private object HandleScreenshot( JObject parameters )
        {
            ScreenshotResult capture;
            try
            {
                capture = ScreenshotCapture.TakeScreenshot( parameters?["highlight"] );
            }
            catch ( ScreenshotBusyException ex )
            {
                return new { success = false, error = ex.Message };
            }

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
                throw new CallerErrorException( "Code is required" );
            }


            try
            {
                var result = CodeExecutor.Execute( code );
                return result;
            }
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
            {
                Main.Log( $"Code execution failed: {ex.Message}" );
                return new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                };
            }
        }

        private object HandleModifyComponent( JObject parameters )
        {
            var path = parameters?["path"]?.ToString();
            var instanceId = ReadInt( parameters, "instanceId" );
            var componentType = parameters?["component"]?.ToString();
            var properties = parameters?["properties"] as JObject;

            if ( ( string.IsNullOrEmpty( path ) && !instanceId.HasValue ) || string.IsNullOrEmpty( componentType ) || properties == null )
            {
                throw new CallerErrorException( "Path or instanceId, component type, and properties are required" );
            }

            return StateModifier.ModifyComponent( path, instanceId, componentType, properties );
        }

        private object HandleTeleportPlayer( JObject parameters )
        {
            var x = ReadFloat( parameters, "x" );
            var y = ReadFloat( parameters, "y" );
            var z = ReadFloat( parameters, "z" );

            if ( !x.HasValue || !y.HasValue )
            {
                throw new CallerErrorException( "X and Y coordinates are required" );
            }

            var playerNum = ReadInt( parameters, "playerNum" ) ?? 0;
            return StateModifier.TeleportPlayer( x.Value, y.Value, z, playerNum );
        }

        private object HandleSetPlayerHealth( JObject parameters )
        {
            var health = ReadInt( parameters, "health" );

            if ( !health.HasValue )
            {
                throw new CallerErrorException( "Health value is required" );
            }

            var playerNum = ReadInt( parameters, "playerNum" ) ?? 0;
            return StateModifier.SetPlayerHealth( health.Value, playerNum );
        }

        private object HandleSpawnEntity( JObject parameters )
        {
            var entityType = parameters?["type"]?.ToString();
            var x = ReadFloat( parameters, "x" );
            var y = ReadFloat( parameters, "y" );
            var xI = ReadFloat( parameters, "xI" );
            var yI = ReadFloat( parameters, "yI" );

            if ( string.IsNullOrEmpty( entityType ) || !x.HasValue || !y.HasValue )
            {
                throw new CallerErrorException( "Entity type and coordinates are required" );
            }

            var ignoreSuppression = ReadBool( parameters, "ignoreSuppression" ) ?? false;
            return StateModifier.SpawnEntity( entityType, x.Value, y.Value, xI ?? 0f, yI ?? 0f, ignoreSuppression );
        }

        private object HandleSetGameSpeed( JObject parameters )
        {
            var speed = ReadFloat( parameters, "speed" );

            if ( !speed.HasValue )
            {
                throw new CallerErrorException( "Speed value is required" );
            }

            return StateModifier.SetGameSpeed( speed.Value );
        }

        private object HandleGoToLevel( JObject parameters )
        {
            var campaignIndex = ReadInt( parameters, "campaignIndex" );
            var levelIndex = ReadInt( parameters, "levelIndex" );

            if ( !campaignIndex.HasValue || !levelIndex.HasValue )
            {
                throw new CallerErrorException( "Campaign index and level index are required" );
            }

            return StateModifier.GoToLevelAndWait( campaignIndex.Value, levelIndex.Value );
        }

        private object HandleListCampaigns( JObject parameters )
        {
            return StateModifier.ListCampaigns();
        }

        private object HandleExecuteScript( JObject parameters )
        {
            var source = parameters?["source"]?.ToString();
            var name = parameters?["name"]?.ToString();

            if ( string.IsNullOrEmpty( source ) )
            {
                throw new CallerErrorException( "Script source is required" );
            }
            if ( string.IsNullOrEmpty( name ) )
            {
                throw new CallerErrorException( "Script name is required" );
            }

            Dictionary<string, string> args = null;
            var argsToken = parameters?["args"] as JObject;
            if ( argsToken != null )
            {
                args = new Dictionary<string, string>();
                foreach ( var prop in argsToken.Properties() )
                {
                    args[prop.Name] = ScriptArgText( prop.Value );
                }
            }

            var origin = parameters?["origin"]?.ToString();
            if ( string.IsNullOrEmpty( origin ) )
            {
                origin = null;
            }

            return ScriptManager.ExecuteScript( name, source, args, origin );
        }

        private static string ScriptArgText( JToken value )
        {
            return value.Type == JTokenType.String ? (string)value : value.ToString( Formatting.None );
        }

        private object HandleCompileScript( JObject parameters )
        {
            var source = parameters?["source"]?.ToString();
            var name = parameters?["name"]?.ToString();

            if ( string.IsNullOrEmpty( source ) )
            {
                throw new CallerErrorException( "Script source is required" );
            }
            if ( string.IsNullOrEmpty( name ) )
            {
                throw new CallerErrorException( "Script name is required" );
            }

            return ScriptManager.CompileOnly( name, source );
        }

        private object HandleUnloadScript( JObject parameters )
        {
            var name = parameters?["name"]?.ToString();

            if ( string.IsNullOrEmpty( name ) )
            {
                throw new CallerErrorException( "Script name is required" );
            }

            return ScriptManager.UnloadScript( name );
        }

        private object HandleListActiveScripts( JObject parameters )
        {
            return ScriptManager.ListActiveScripts();
        }

        private object HandleGameState( JObject parameters )
        {
            Func<object> read = () =>
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
                    isInGame = StateModifier.NotPlayingReason() == null;
                    levelFinished = GameModeController.IsLevelFinished();
                }
                catch { }

                // Off a level the game still holds the last one it left, or the next one it will load.
                if ( !isInGame )
                {
                    campaignName = null;
                    campaignDisplay = null;
                    levelIndex = null;
                    levelNumber = null;
                    levelName = null;
                }

                try
                {
                    pauseState = PauseController.pauseStatus.ToString();
                }
                catch { }

                bool ironBro = false;
                try
                {
                    ironBro = GameModeController.IsHardcoreMode;
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
                            var slot = StateModifier.ReadSlot( i );
                            if ( slot.Character != null )
                            {
                                var character = slot.Character;
                                players.Add( new
                                {
                                    playerIndex = i,
                                    playerNum = player.playerNum,
                                    broName = character.gameObject.name,
                                    heroType = character.heroType.ToString(),
                                    className = character.GetType().Name,
                                    position = new { x = character.X, y = character.Y },
                                    health = character.health,
                                    isAlive = slot.State == "alive",
                                    state = slot.State,
                                    piloting = slot.Piloting,
                                    lives = player.Lives
                                } );
                            }
                            else if ( slot.State != "empty" )
                            {
                                bool live = player != null;
                                players.Add( new
                                {
                                    playerIndex = i,
                                    playerNum = live ? player.playerNum : i,
                                    broName = (string)null,
                                    heroType = live ? player.heroType.ToString() : null,
                                    className = (string)null,
                                    position = (object)null,
                                    health = 0,
                                    isAlive = false,
                                    state = slot.State,
                                    piloting = (string)null,
                                    lives = live ? player.Lives : (int?)null
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
                    mainThreadAnswered = true,
                    scene = scene.name,
                    isCampaignScene,
                    gameMode,
                    isInGame,
                    levelFinished,
                    pauseState,
                    ironBro,
                    campaignDisplay,
                    campaignName,
                    levelIndex,
                    levelNumber,
                    levelName,
                    timeScale,
                    playerCount = players.Count,
                    players
                };
            };

            try
            {
                return OnMainThread( read );
            }
            catch ( TimeoutException ex )
            {
                return new { mainThreadAnswered = false, error = MainThreadSilent( ex ) };
            }
            catch ( MainThreadDispatcher.MainThreadActionRunningException ex )
            {
                return new { mainThreadAnswered = false, error = MainThreadSilent( ex ) };
            }
            catch ( Exception ex )
            {
                Main.Log( $"HandleGameState - Fatal error: {ex.Message}\n{ex.StackTrace}" );
                return new { success = false, mainThreadAnswered = true, error = ex.Message };
            }
        }

        private static string MainThreadSilent( Exception ex )
        {
            return $"The game's main thread did not answer, so its state could not be read - it is hung or busy: {ex.Message}";
        }


        private object HandleSwapBro( JObject parameters )
        {
            var broName = parameters?["broName"]?.ToString();
            if ( string.IsNullOrEmpty( broName ) )
            {
                throw new CallerErrorException( "Bro name is required" );
            }

            var playerNum = ReadInt( parameters, "playerNum" ) ?? 0;
            var ignoreUnlocks = ReadBool( parameters, "ignoreUnlocks" ) ?? false;
            return StateModifier.SwapBro( broName, playerNum, ignoreUnlocks );
        }

        private object HandleSetBro( JObject parameters )
        {
            var broName = parameters?["broName"]?.ToString();
            if ( string.IsNullOrEmpty( broName ) )
            {
                throw new CallerErrorException( "Bro name is required" );
            }

            var playerNum = ReadInt( parameters, "playerNum" ) ?? 0;
            return StateModifier.SetBro( broName, playerNum );
        }

        private object HandleListBros( JObject parameters )
        {
            var playerNum = ReadInt( parameters, "playerNum" ) ?? 0;
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
                        long since = ReadLong( parameters, "since", long.MinValue, long.MaxValue ) ?? 0;
                        MainThreadDispatcher.EnqueueAndWait( () => { result = SpawnTrace.Read( since ); }, 5000 );
                        return result;

                    case "stop":
                        MainThreadDispatcher.EnqueueAndWait( () => { result = SpawnTrace.Stop(); }, 5000 );
                        return result;

                    default:
                        return new { success = false, error = "action must be 'start', 'read' or 'stop'" };
                }
            }
            catch ( ParamException )
            {
                throw;
            }
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
            {
                Main.Log( $"HandleTraceSpawns error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }
    }
}
