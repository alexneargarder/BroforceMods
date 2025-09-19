using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public class MessageHandler
    {
        private readonly Dictionary<string, Func<JObject, object>> handlers;

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
                ["simulate_input"] = HandleSimulateInput
            };
        }

        public string HandleMessage( string message )
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
            
            // Debug logging
            
            var rootObjects = GameObjectInspector.GetRootGameObjects( includeInactive );

            // Limit results to prevent response size issues
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

            // Check for detailed parameter (default to false for lightweight response)
            var detailed = parameters?["detailed"]?.ToObject<bool>() ?? false;

            // Can't use GameObject.Find() as it only searches active scene hierarchy
            // Need to search through all objects like we do in list
            GameObject go = null;
            
            // First try the active scene
            go = GameObject.Find( path );
            
            // If not found, search through all objects
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
            
            if ( string.IsNullOrEmpty( path ) )
            {
                throw new ArgumentException( "GameObject path is required" );
            }
            
            if ( string.IsNullOrEmpty( componentType ) )
            {
                throw new ArgumentException( "Component type is required" );
            }

            // Find the GameObject first
            GameObject go = null;
            
            // First try the active scene
            go = GameObject.Find( path );
            
            // If not found, search through all objects
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
            
            if ( go == null )
            {
                throw new ArgumentException( $"GameObject not found: {path}" );
            }

            return GameObjectInspector.InspectSpecificComponent( go, componentType );
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
                            
                            // Don't do deep inspection, just basic info for now
                            results.Add( new
                            {
                                playerIndex = i,
                                playerNum = player.playerNum,
                                name = go.name,
                                position = new { x = go.transform.position.x, y = go.transform.position.y },
                                isActive = go.activeSelf
                                // Commenting out deep inspection until we fix the crash
                                // details = GameObjectInspector.InspectGameObject( go )
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
                
                var enemies = UnityEngine.Object.FindObjectsOfType<Unit>()
                    .Where( u => u != null && u.gameObject != null &&
                           (u.GetType().Name.Contains( "Mook" ) || u.GetType().Name.Contains( "Enemy" )) )
                    .Select( e => 
                    {
                        try
                        {
                            return new
                            {
                                name = e.name,
                                type = e.GetType().Name,
                                position = new { x = e.X, y = e.Y },
                                health = e.health,
                                isAlive = e.health > 0
                            };
                        }
                        catch ( Exception ex )
                        {
                            Main.Log( $"HandleListEnemies - Error processing enemy {e.name}: {ex.Message}" );
                            return null;
                        }
                    } )
                    .Where( e => e != null )
                    .ToArray();

                return new { count = enemies.Length, enemies };
            }
            catch ( Exception ex )
            {
                Main.Log( $"HandleListEnemies - Fatal error: {ex.Message}\n{ex.StackTrace}" );
                return new { error = ex.Message, count = 0, enemies = new object[0] };
            }
        }

        private object HandleScreenshot( JObject parameters )
        {
            var screenshotPath = ScreenshotCapture.TakeScreenshot();

            return new
            {
                success = true,
                path = screenshotPath,
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

        private string GetGameObjectPath( GameObject go )
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

            return StateModifier.TeleportPlayer(x.Value, y.Value, z);
        }

        private object HandleSetPlayerHealth(JObject parameters)
        {
            var health = parameters?["health"]?.Value<int>();

            if (!health.HasValue)
            {
                throw new ArgumentException("Health value is required");
            }

            return StateModifier.SetPlayerHealth(health.Value);
        }

        private object HandleSpawnEntity(JObject parameters)
        {
            var entityType = parameters?["type"]?.ToString();
            var x = parameters?["x"]?.Value<float>();
            var y = parameters?["y"]?.Value<float>();

            if (string.IsNullOrEmpty(entityType) || !x.HasValue || !y.HasValue)
            {
                throw new ArgumentException("Entity type and coordinates are required");
            }

            return StateModifier.SpawnEntity(entityType, x.Value, y.Value);
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
            if ( string.IsNullOrEmpty( action ) )
            {
                throw new ArgumentException( "Input action is required" );
            }
            
            var duration = parameters?["duration"]?.ToObject<int?>();
            var playerNum = parameters?["player"]?.ToObject<int?>();
            var count = parameters?["count"]?.ToObject<int?>();
            var interval = parameters?["interval"]?.ToObject<int?>();
            
            return InputSimulator.SimulateInput( action, duration, playerNum, count, interval );
        }
    }
}