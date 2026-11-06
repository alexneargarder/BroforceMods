using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using RocketLib.Collections;
using RocketLib.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity_Inspector_Mod
{
    public static class StateModifier
    {
        public static object ModifyComponent( string gameObjectPath, string componentType, JObject properties )
        {
            try
            {
                object result = null;

                // A struct link is read, written and stored back as one unit; off the main thread the
                // game can set a sibling field between the read and the store and lose it.
                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    result = ModifyComponentOnMainThread( gameObjectPath, componentType, properties );
                }, 5000 );

                return result;
            }
            catch ( Exception ex )
            {
                Main.Log( $"ModifyComponent error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }

        private static object ModifyComponentOnMainThread( string gameObjectPath, string componentType, JObject properties )
        {
            if ( properties == null || properties.Count == 0 )
            {
                return new { success = false, error = "No properties were given - pass at least one member name and value to write" };
            }

            GameObject go = MessageHandler.FindGameObjectByPath( gameObjectPath );
            if ( go == null )
            {
                return new { success = false, error = $"GameObject not found at path: {gameObjectPath}" };
            }

            Component component = GameObjectInspector.FindComponent( go, componentType );

            if ( component == null )
            {
                return new { success = false, error = $"Component {componentType} not found on {gameObjectPath}" };
            }

            var modifiedProperties = new List<string>();
            var errors = new List<object>();

            foreach ( var prop in properties )
            {
                string propName = prop.Key;
                JToken propValue = prop.Value;

                string reason;
                string[] suggestions;
                string failure = SetPropertyValue( component, propName, propValue, out reason, out suggestions );
                if ( failure == null )
                {
                    modifiedProperties.Add( propName );
                }
                else if ( suggestions != null )
                {
                    errors.Add( new { property = propName, reason, message = failure, suggestions } );
                }
                else
                {
                    errors.Add( new { property = propName, reason, message = failure } );
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

        private static string SetPropertyValue( object target, string propertyName, JToken value, out string reason, out string[] suggestions )
        {
            reason = null;
            suggestions = null;

            if ( !MemberPath.IsPath( propertyName ) )
            {
                return SetMemberValue( target, propertyName, propertyName, value, out reason, out suggestions );
            }

            try
            {
                MemberPathResolution resolution = MemberPath.ResolveOwner( target, propertyName );
                if ( !resolution.Success )
                {
                    reason = resolution.Reason;
                    suggestions = resolution.Suggestions;
                    return resolution.Error;
                }

                // Refused before anything is written: a write past a boxed struct that cannot be
                // stored back would otherwise report success and change nothing.
                string blocked = MemberPath.CheckWriteBack( resolution, out reason );
                if ( blocked != null )
                {
                    return blocked;
                }

                string failure = SetMemberValue( resolution.Owner, resolution.Leaf, propertyName, value, out reason, out suggestions );
                if ( failure == null )
                {
                    MemberPath.WriteBack( resolution );
                }

                return failure;
            }
            catch ( Exception ex )
            {
                // One property's failure, not the whole call's: the properties already written stay reported.
                Exception failure = MemberPath.Unwrap( ex );
                Main.Log( $"SetPropertyValue error for {propertyName}: {failure.Message}" );
                reason = "error";
                suggestions = null;
                return failure.Message;
            }
        }

        private static string SetMemberValue( object target, string memberName, string reportedName, JToken value, out string reason, out string[] suggestions )
        {
            const BindingFlags memberFlags = BindingFlags.DeclaredOnly | BindingFlags.Public
                                                                       | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            reason = null;
            suggestions = null;

            try
            {
                Type type = target.GetType();

                for ( Type t = type; t != null; t = t.BaseType )
                {
                    FieldInfo field = t.GetField( memberName, memberFlags );
                    if ( field != null )
                    {
                        if ( field.IsLiteral )
                        {
                            reason = "read-only";
                            return $"Field {t.Name}.{field.Name} is a constant";
                        }

                        object convertedValue = ConvertValue( value, field.FieldType );
                        field.SetValue( field.IsStatic ? null : target, convertedValue );
                        return null;
                    }

                    PropertyInfo property = t.GetProperty( memberName, memberFlags );
                    if ( property != null )
                    {
                        if ( !property.CanWrite )
                        {
                            reason = "read-only";
                            return $"Property {t.Name}.{property.Name} has no setter";
                        }

                        MethodInfo setter = property.GetSetMethod( true );
                        object convertedValue = ConvertValue( value, property.PropertyType );
                        property.SetValue( setter != null && setter.IsStatic ? null : target, convertedValue, null );
                        return null;
                    }
                }

                reason = "not-found";
                suggestions = MemberPath.Suggest( type, memberName, MemberPath.PathPrefix( reportedName ) );
                return memberName == reportedName
                    ? $"No field or property named {memberName} on {type.Name} or any of its base types"
                    : $"No field or property named {memberName} on {type.Name} or any of its base types (resolving {reportedName})";
            }
            catch ( Exception ex )
            {
                // A setter that threw arrives wrapped, and its own message is the one worth reporting.
                Exception failure = MemberPath.Unwrap( ex );
                Main.Log( $"SetPropertyValue error for {reportedName}: {failure.Message}" );
                reason = "error";
                suggestions = null;
                return failure.Message;
            }
        }

        private static object ConvertValue( JToken value, Type targetType )
        {
            if ( value == null || value.Type == JTokenType.Null )
            {
                return null;
            }

            if ( targetType == typeof( Vector3 ) )
            {
                if ( value.Type == JTokenType.Object )
                {
                    return new Vector3(
                        value["x"]?.Value<float>() ?? 0,
                        value["y"]?.Value<float>() ?? 0,
                        value["z"]?.Value<float>() ?? 0
                    );
                }
            }
            else if ( targetType == typeof( Vector2 ) )
            {
                if ( value.Type == JTokenType.Object )
                {
                    return new Vector2(
                        value["x"]?.Value<float>() ?? 0,
                        value["y"]?.Value<float>() ?? 0
                    );
                }
            }
            else if ( targetType == typeof( Color ) )
            {
                if ( value.Type == JTokenType.Object )
                {
                    return new Color(
                        value["r"]?.Value<float>() ?? 0,
                        value["g"]?.Value<float>() ?? 0,
                        value["b"]?.Value<float>() ?? 0,
                        value["a"]?.Value<float>() ?? 1
                    );
                }
            }
            else if ( targetType == typeof( Quaternion ) )
            {
                if ( value.Type == JTokenType.Object )
                {
                    if ( value["euler"] != null )
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

            return value.ToObject( targetType );
        }

        #region Level and Player Slots
        public static string NotPlayingReason()
        {
            string scene = SceneManager.GetActiveScene().name;

            if ( LevelSelectionController.IsMainMenuScene )
            {
                return $"no level is being played - the main menu ({scene}) is showing";
            }
            if ( LevelSelectionController.IsWorldMapScene )
            {
                return $"no level is being played - the world map ({scene}) is showing";
            }
            if ( LevelSelectionController.IsLoadingScene )
            {
                return $"no level is being played - a level is loading ({scene})";
            }

            string levelScene;
            try { levelScene = LevelSelectionController.CurrentGameModeScene; }
            catch { levelScene = LevelSelectionController.CampaignScene; }

            if ( !string.Equals( scene, levelScene, StringComparison.OrdinalIgnoreCase ) )
            {
                return $"no level is being played - scene {scene} is not a level";
            }
            if ( Map.MapData == null )
            {
                return $"no level is loaded (scene {scene})";
            }
            return null;
        }

        public sealed class SlotState
        {
            public int Index;
            public TestVanDammeAnim Character;
            public string State;
            public string Piloting;
        }

        public static SlotState ReadSlot( int index )
        {
            var slot = new SlotState { Index = index, State = "empty" };
            Player[] players = HeroController.players;
            if ( players == null || index < 0 || index >= players.Length )
            {
                return slot;
            }

            // The join screen keeps the previous level's destroyed Player in the slot.
            if ( players[index] == null )
            {
                if ( HeroController.IsPlaying( index ) )
                {
                    slot.State = "joined";
                }
                return slot;
            }

            TestVanDammeAnim character = players[index].character;
            if ( character == null )
            {
                slot.State = "respawning";
                return slot;
            }

            slot.Character = character;
            slot.State = CharacterIsAlive( character ) ? "alive" : "dead";
            if ( character.pilottedUnit != null )
            {
                slot.Piloting = VehicleName( character.pilottedUnit );
            }

            return slot;
        }

        public static bool CharacterIsAlive( TestVanDammeAnim character )
        {
            // Some IsAlive overrides read Time.time, which may throw off the main thread.
            try { return character.IsAlive(); }
            catch { return character.health > 0; }
        }

        public static string VehicleName( Unit vehicle )
        {
            try { return vehicle.name; }
            catch { return vehicle.GetType().Name; }
        }

        public static string SlotRefusal( SlotState slot, bool refusePiloting )
        {
            switch ( slot.State )
            {
                case "empty": return $"no player in slot {slot.Index}";
                case "joined": return $"player {slot.Index} has joined but has no bro yet";
                case "respawning": return $"player {slot.Index} is dead and waiting to respawn";
                case "dead": return $"player {slot.Index} is dead";
            }

            if ( refusePiloting && slot.Piloting != null )
            {
                return $"player {slot.Index} is piloting {slot.Piloting}";
            }
            return null;
        }

        public static string SlotReasonCode( SlotState slot )
        {
            switch ( slot.State )
            {
                case "empty":
                case "joined": return "no-player";
                case "respawning":
                case "dead": return "player-dead";
            }

            return slot.Piloting != null ? "piloting" : null;
        }

        private static object SkippedSlot( SlotState slot, string refusal )
        {
            return new { playerNum = slot.Index, reason = SlotReasonCode( slot ), message = refusal };
        }
        #endregion

        // High-level convenience methods
        public static object TeleportPlayer( float x, float y, float? z = null, int playerNum = 0 )
        {
            try
            {
                if ( playerNum < -1 || playerNum >= Nums.PLAYER_MAX )
                {
                    return new { success = false, error = "Player number must be 0-3, or -1 for all players" };
                }

                object result = null;
                MainThreadDispatcher.EnqueueAndWait( () => { result = TeleportOnMainThread( x, y, z, playerNum ); }, 5000 );
                return result;
            }
            catch ( Exception ex )
            {
                Main.Log( $"TeleportPlayer error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }

        private static object TeleportOnMainThread( float x, float y, float? z, int playerNum )
        {
            string notPlaying = NotPlayingReason();
            if ( notPlaying != null )
            {
                return new { success = false, error = $"Cannot teleport: {notPlaying}", reason = "no-level" };
            }

            // Map.IsWithinBounds passes an infinite x or y, since the column cast of infinity wraps.
            if ( float.IsNaN( x ) || float.IsInfinity( x ) || float.IsNaN( y ) || float.IsInfinity( y ) || !Map.IsWithinBounds( new Vector3( x, y, 0f ) ) )
            {
                return new
                {
                    success = false,
                    error = $"({x}, {y}) is outside the map: {DescribeMapBounds()}",
                    reason = "out-of-bounds",
                    bounds = new { minX = 0f, maxX = MapMaxX(), minY = 0f, maxY = MapMaxY() }
                };
            }

            string zNote = z.HasValue ? "z cannot be set: the game rewrites it from the unit's own z offset every frame" : null;

            if ( playerNum == -1 )
            {
                var results = new List<object>();
                var skipped = new List<object>();
                object lastPosition = null;

                for ( int i = 0; i < Nums.PLAYER_MAX; i++ )
                {
                    SlotState slot = ReadSlot( i );
                    string refusal = SlotRefusal( slot, true );
                    if ( refusal != null )
                    {
                        skipped.Add( SkippedSlot( slot, refusal ) );
                        continue;
                    }

                    lastPosition = MoveCharacter( slot.Character, x, y );
                    results.Add( new { playerNum = i, playerName = slot.Character.name, newPosition = lastPosition } );
                }

                if ( results.Count == 0 )
                {
                    return new { success = false, error = "No player could be teleported", skipped = skipped.ToArray() };
                }

                var all = JObject.FromObject( new
                {
                    success = true,
                    newPosition = lastPosition,
                    playerNum = -1,
                    results = results.ToArray(),
                    skipped = skipped.ToArray()
                } );
                if ( zNote != null )
                {
                    all["note"] = zNote;
                }
                return all;
            }

            SlotState target = ReadSlot( playerNum );
            string targetRefusal = SlotRefusal( target, true );
            if ( targetRefusal != null )
            {
                return new { success = false, error = $"Cannot teleport: {targetRefusal}", reason = SlotReasonCode( target ) };
            }

            var reply = JObject.FromObject( new
            {
                success = true,
                newPosition = MoveCharacter( target.Character, x, y ),
                playerNum = playerNum,
                playerName = target.Character.name
            } );
            if ( zNote != null )
            {
                reply["note"] = zNote;
            }
            return reply;
        }

        private static object MoveCharacter( TestVanDammeAnim character, float x, float y )
        {
            character.SetXY( x, y );
            character.SetPosition();
            character.xI = 0f;
            character.yI = 0f;
            character.xIBlast = 0f;
            character.yIBlast = 0f;

            Vector3 held = character.transform.position;
            return new { x = held.x, y = held.y, z = held.z };
        }

        private static float MapMaxX()
        {
            return Map.Width * Map.TileSize - Map.HalfTileSize;
        }

        private static float MapMaxY()
        {
            return Map.Height * Map.TileSize - Map.HalfTileSize;
        }

        private static string DescribeMapBounds()
        {
            return $"the map spans x from 0 to below {MapMaxX()} and y from 0 to below {MapMaxY()}";
        }

        public static object SetPlayerHealth( int health, int playerNum = 0 )
        {
            try
            {
                if ( playerNum < -1 || playerNum >= Nums.PLAYER_MAX )
                {
                    return new { success = false, error = "Player number must be 0-3, or -1 for all players" };
                }

                object result = null;
                MainThreadDispatcher.EnqueueAndWait( () => { result = SetPlayerHealthOnMainThread( health, playerNum ); }, 5000 );
                return result;
            }
            catch ( Exception ex )
            {
                Main.Log( $"SetPlayerHealth error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }

        private static object SetPlayerHealthOnMainThread( int health, int playerNum )
        {
            string notPlaying = NotPlayingReason();
            if ( notPlaying != null )
            {
                return new { success = false, error = $"Cannot set health: {notPlaying}", reason = "no-level" };
            }

            if ( playerNum == -1 )
            {
                var results = new List<object>();
                var skipped = new List<object>();
                string failure = null;

                for ( int i = 0; i < Nums.PLAYER_MAX; i++ )
                {
                    SlotState slot = ReadSlot( i );
                    // Killing a hidden pilot leaves the vehicle holding a corpse that the discharge later revives.
                    string refusal = SlotRefusal( slot, health <= 0 );
                    if ( refusal != null )
                    {
                        skipped.Add( SkippedSlot( slot, refusal ) );
                        continue;
                    }

                    string error;
                    bool killed = ApplyHealth( slot, health, out error );
                    if ( error != null )
                    {
                        failure = failure == null ? error : failure + "; " + error;
                    }
                    results.Add( new { playerNum = i, playerName = slot.Character.name, health = slot.Character.health, killed } );
                }

                if ( results.Count == 0 )
                {
                    return new { success = false, error = "No player's health could be set", skipped = skipped.ToArray() };
                }

                var all = JObject.FromObject( new
                {
                    success = failure == null,
                    health = health,
                    playerNum = -1,
                    results = results.ToArray(),
                    skipped = skipped.ToArray()
                } );
                if ( failure != null )
                {
                    all["error"] = failure;
                }
                return all;
            }

            SlotState target = ReadSlot( playerNum );
            string targetRefusal = SlotRefusal( target, health <= 0 );
            if ( targetRefusal != null )
            {
                return new { success = false, error = $"Cannot set health: {targetRefusal}", reason = SlotReasonCode( target ) };
            }

            string targetError;
            bool targetKilled = ApplyHealth( target, health, out targetError );
            var reply = JObject.FromObject( new
            {
                success = targetError == null,
                health = target.Character.health,
                killed = targetKilled,
                playerNum = playerNum,
                playerName = target.Character.name
            } );
            if ( targetError != null )
            {
                reply["error"] = targetError;
            }
            return reply;
        }

        private static bool ApplyHealth( SlotState slot, int health, out string error )
        {
            error = null;
            TestVanDammeAnim character = slot.Character;

            if ( health > 0 )
            {
                character.health = health;
                return false;
            }

            // The hit itself clears some damage absorbers (a parachute closes).
            string absorber = DamageAbsorber( character );

            character.Damage( character.health, DamageType.Crush, 0f, 0f, 0, null, character.X, character.Y );

            bool killed = character.health <= 0 || character.actionState == ActionState.Dead;
            if ( !killed )
            {
                error = $"player {slot.Index} survived: the game's damage path absorbed it" + ( absorber != null ? $" ({absorber})" : "" );
            }

            return killed;
        }

        private static FieldInfo _flexInvulnerabilityField;

        private static string DamageAbsorber( TestVanDammeAnim character )
        {
            try
            {
                if ( character.inseminatorUnit != null )
                {
                    return "a facehugger takes the damage";
                }
                if ( character.IsParachuteActive )
                {
                    return "an open parachute";
                }
                if ( character.hasBeenCoverInAcid )
                {
                    return "an acid coating";
                }

                if ( character is BroBase )
                {
                    if ( _flexInvulnerabilityField == null )
                    {
                        _flexInvulnerabilityField = typeof( BroBase ).GetField( "flexInvulnerability", BindingFlags.Instance | BindingFlags.NonPublic );
                    }

                    if ( _flexInvulnerabilityField != null && (float)_flexInvulnerabilityField.GetValue( character ) > 0f )
                    {
                        return "flex invulnerability";
                    }
                }
            }
            catch ( Exception ex )
            {
                Main.Log( $"Reading what absorbs damage failed: {ex.Message}" );
            }

            return null;
        }

        public static object SpawnEntity( string entityType, float x, float y, float xI = 0f, float yI = 0f, bool ignoreSuppression = false )
        {
            try
            {
                object result = null;
                bool? suppressionFound = null;
                string suppressionNote = null;

                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    if ( !ignoreSuppression )
                    {
                        result = SpawnEntityOnMainThread( entityType, x, y, xI, yI );
                        return;
                    }

                    string missing;
                    UtilityModSpawnSetting setting = FindUtilityModSpawnSetting( out missing );
                    if ( setting == null )
                    {
                        suppressionNote = $"{missing} - its disableEnemySpawn setting could not be read, so the spawn ran unchanged";
                        result = SpawnEntityOnMainThread( entityType, x, y, xI, yI );
                        return;
                    }

                    bool previous = setting.Read();
                    suppressionFound = setting.Enabled && previous;
                    setting.Write( false );
                    try
                    {
                        result = SpawnEntityOnMainThread( entityType, x, y, xI, yI );
                    }
                    finally
                    {
                        setting.Write( previous );
                    }
                }, 5000 );

                if ( !ignoreSuppression )
                {
                    return result;
                }

                JObject reply = JObject.FromObject( result );
                reply["suppressionFound"] = suppressionFound.HasValue ? new JValue( suppressionFound.Value ) : JValue.CreateNull();
                if ( suppressionNote != null )
                {
                    reply["suppressionNote"] = suppressionNote;
                }
                return reply;
            }
            catch ( Exception ex )
            {
                Main.Log( $"SpawnEntity error: {ex.Message}" );
                return new { success = false, error = ex.Message, stackTrace = ex.StackTrace };
            }
        }

        private static object SpawnEntityOnMainThread( string entityType, float x, float y, float xI, float yI )
        {
            Map map = Map.Instance;

            if ( map == null || map.activeTheme == null )
            {
                return new { success = false, error = "No level is loaded - a level must be loaded before entities can be spawned" };
            }

            UnitType unitType = ResolveUnitType( entityType );

            if ( unitType == UnitType.None )
            {
                if ( NamesBro( entityType ) )
                {
                    return new
                    {
                        success = false,
                        error = "'Bro' is a RocketLib unit type but cannot be spawned here - bros come from the character roster, so use swap_bro to change the bro a player has"
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
            catch ( Exception ex )
            {
                prefabFailure = $"{ex.GetType().Name}: {ex.Message}";
            }

            if ( prefab == null )
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
            bool networked = false;
            string mookSpawn = null;

            try
            {
                if ( unitType == UnitType.Facehugger )
                {
                    spawned = Map.SpawnFaceHugger( x, y, xI, yI );
                }
                else if ( unitType == UnitType.Screecher )
                {
                    spawned = Map.SpawnFaceBaneling( x, y, xI, yI );
                }
                else if ( prefab is Mook && InstantiationController.GetPrefabIndex( prefab.gameObject ) >= 0 )
                {
                    mookSpawn = "SpawnMook_Networked";
                    spawned = MapController.SpawnMook_Networked( prefab as Mook, x, y, xI, yI, false, false, false, false, true );
                    networked = true;
                }
                else if ( prefab is Mook )
                {
                    // SpawnMook_Networked dereferences null for a prefab outside the game's networked prefab list.
                    mookSpawn = "SpawnMook_Local";
                    spawned = SpawnMookLocal( prefab as Mook, x, y, xI, yI );
                }
                else
                {
                    spawned = SpawnUnitLocal( prefab, x, y, xI, yI );
                }
            }
            catch ( Exception ex )
            {
                return new
                {
                    success = false,
                    error = $"Spawning '{displayName}' threw {ex.GetType().Name}: {ex.Message}",
                    unitType = unitType.ToString()
                };
            }

            if ( spawned == null )
            {
                if ( mookSpawn != null )
                {
                    return new
                    {
                        success = false,
                        error = $"{( networked ? "Networked" : "Local" )} spawn of '{displayName}' returned no instance{DescribeSpawnMookPrefixes( mookSpawn )}"
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
        private static Unit SpawnUnitLocal( Unit prefab, float x, float y, float xI, float yI )
        {
            Unit unit = UnityEngine.Object.Instantiate<Unit>( prefab, new Vector3( x, y, 0f ), Quaternion.identity );

            if ( unit == null )
            {
                return null;
            }

            PlaceLocalUnit( unit );
            unit.xI = xI;
            unit.yI = yI;
            return unit;
        }

        private static Unit SpawnMookLocal( Mook prefab, float x, float y, float xI, float yI )
        {
            Mook mook = MapController.SpawnMook_Local( prefab, x, y, xI, yI, false, false, false, false, true );

            if ( mook == null )
            {
                return null;
            }

            PlaceLocalUnit( mook );
            return mook;
        }

        private static void PlaceLocalUnit( Unit unit )
        {
            if ( Map.Instance != null )
            {
                unit.transform.parent = Map.Instance.transform;
            }

            Registry.RegisterDeterminsiticGameObject( unit.gameObject );
        }

        private static UnitType ResolveUnitType( string requested )
        {
            string trimmed = ( requested ?? string.Empty ).Trim();

            if ( trimmed.Length == 0 )
            {
                return UnitType.None;
            }

            UnitType byDisplayName = UnitTypes.ToUnitType( trimmed );

            if ( IsSpawnable( byDisplayName ) )
            {
                return byDisplayName;
            }

            string normalized = NormalizeUnitName( trimmed );

            foreach ( UnitType candidate in Enum.GetValues( typeof( UnitType ) ) )
            {
                if ( !IsSpawnable( candidate ) )
                {
                    continue;
                }

                if ( normalized == NormalizeUnitName( candidate.ToString() ) ||
                     normalized == NormalizeUnitName( candidate.ToDisplayString() ) )
                {
                    return candidate;
                }
            }

            return UnitType.None;
        }

        // None names nothing, and Bro has no prefab of its own - bros come from the character roster.
        private static bool IsSpawnable( UnitType type )
        {
            return type != UnitType.None && type != UnitType.Bro;
        }

        // Bro resolves nowhere, so it would otherwise read as a name that does not exist.
        private static bool NamesBro( string requested )
        {
            string normalized = NormalizeUnitName( ( requested ?? string.Empty ).Trim() );

            return normalized.Length > 0 &&
                   ( normalized == NormalizeUnitName( UnitType.Bro.ToString() ) ||
                     normalized == NormalizeUnitName( UnitType.Bro.ToDisplayString() ) );
        }

        private static string NormalizeUnitName( string name )
        {
            var normalized = new StringBuilder( name.Length );

            foreach ( char character in name )
            {
                if ( char.IsLetterOrDigit( character ) )
                {
                    normalized.Append( char.ToLowerInvariant( character ) );
                }
            }

            return normalized.ToString();
        }

        // A prefix that returns false leaves __result null, so the spawn silently produces nothing.
        private static string DescribeSpawnMookPrefixes( string method )
        {
            string[] owners = GetPrefixOwners( new[]
            {
                new KeyValuePair<Type, string>( typeof( MapController ), method )
            } );

            if ( owners.Length == 0 )
            {
                return string.Empty;
            }

            return $" - a Harmony prefix from {string.Join( ", ", owners )} may have suppressed the spawn (Utility Mod's disableEnemySpawn does this)";
        }

        public static string[] GetEnemySpawnPrefixOwners()
        {
            return GetPrefixOwners( new[]
            {
                new KeyValuePair<Type, string>( typeof( MapController ), "SpawnMook_Networked" ),
                new KeyValuePair<Type, string>( typeof( MapController ), "SpawnMook_Local" ),
                new KeyValuePair<Type, string>( typeof( Map ), "PlaceDoodad" )
            } );
        }

        // Utility Mod registers its prefixes unconditionally and tests the setting inside them, so only the setting says whether spawning is actually suppressed.
        public static string[] GetActiveEnemySpawnSuppressors()
        {
            var suppressors = new List<string>();

            if ( IsUtilityModSuppressingEnemySpawns() )
            {
                suppressors.Add( "Utility Mod" );
            }

            return suppressors.ToArray();
        }

        private static bool IsUtilityModSuppressingEnemySpawns()
        {
            try
            {
                string reason;
                UtilityModSpawnSetting setting = FindUtilityModSpawnSetting( out reason );
                if ( setting == null )
                {
                    if ( reason != null && reason != UtilityModNotLoaded )
                    {
                        Main.Log( $"{reason} - cannot tell whether enemy spawning is suppressed" );
                    }
                    return false;
                }

                return setting.Enabled && setting.Read();
            }
            catch ( Exception ex )
            {
                Main.Log( $"Enemy spawn suppression check error: {ex.Message}" );
                return false;
            }
        }

        private const string UtilityModNotLoaded = "Utility Mod is not loaded";

        private sealed class UtilityModSpawnSetting
        {
            public bool Enabled;
            public object Settings;
            public FieldInfo Field;

            public bool Read()
            {
                return (bool)Field.GetValue( Settings );
            }

            public void Write( bool value )
            {
                Field.SetValue( Settings, value );
            }
        }

        private static UtilityModSpawnSetting FindUtilityModSpawnSetting( out string reason )
        {
            reason = null;

            Type mainType = null;
            foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
            {
                if ( assembly.GetName().Name != "Utility Mod" )
                {
                    continue;
                }

                mainType = assembly.GetType( "Utility_Mod.Main" );
                break;
            }

            if ( mainType == null )
            {
                reason = UtilityModNotLoaded;
                return null;
            }

            BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            FieldInfo enabledField = mainType.GetField( "enabled", staticFlags );
            object enabled = enabledField == null ? null : enabledField.GetValue( null );

            FieldInfo settingsField = mainType.GetField( "settings", staticFlags );
            object settings = settingsField == null ? null : settingsField.GetValue( null );
            if ( settings == null )
            {
                reason = "Utility Mod is loaded but its settings field is missing or null";
                return null;
            }

            FieldInfo disableField = settings.GetType().GetField( "disableEnemySpawn",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
            if ( disableField == null || disableField.FieldType != typeof( bool ) )
            {
                reason = "Utility Mod is loaded but its disableEnemySpawn setting is missing";
                return null;
            }

            return new UtilityModSpawnSetting
            {
                Enabled = enabled is bool && (bool)enabled,
                Settings = settings,
                Field = disableField
            };
        }

        private static string[] GetPrefixOwners( KeyValuePair<Type, string>[] targets )
        {
            var owners = new List<string>();

            foreach ( var target in targets )
            {
                try
                {
                    MethodBase method = AccessTools.Method( target.Key, target.Value );
                    if ( method == null )
                    {
                        continue;
                    }

                    Patches patches = Harmony.GetPatchInfo( method );
                    if ( patches == null || patches.Prefixes == null )
                    {
                        continue;
                    }

                    foreach ( var patch in patches.Prefixes )
                    {
                        if ( patch.owner == SpawnTrace.HarmonyId )
                        {
                            continue;
                        }

                        if ( !string.IsNullOrEmpty( patch.owner ) && !owners.Contains( patch.owner ) )
                        {
                            owners.Add( patch.owner );
                        }
                    }
                }
                catch ( Exception ex )
                {
                    Main.Log( $"Harmony prefix lookup error for {target.Value}: {ex.Message}" );
                }
            }

            return owners.ToArray();
        }

        // The game clamps a unit's per-frame step at 0.0334s, so past about 2x at 60fps units stop speeding up while timers keep scaling.
        public const float MaxGameSpeed = 10f;

        private static FieldInfo _timescaleWhenPausedField;

        public static object SetGameSpeed( float speed )
        {
            try
            {
                if ( float.IsNaN( speed ) || float.IsInfinity( speed ) || speed < 0f || speed > MaxGameSpeed )
                {
                    return new { success = false, error = $"Speed {speed} is out of range - give a number from 0 to {MaxGameSpeed}" };
                }

                Time.timeScale = speed;
                var reply = JObject.FromObject( new { success = true, timeScale = Time.timeScale } );

                // The offline pause menu holds timeScale at 1 and restores the value it saved when it closes.
                PauseController pause = PauseController.instance;
                if ( PauseController.pauseStatus == PauseStatus.MenuPause && Connect.IsOffline && pause != null )
                {
                    if ( _timescaleWhenPausedField == null )
                    {
                        _timescaleWhenPausedField = typeof( PauseController ).GetField( "timescaleWhenPaused", BindingFlags.Instance | BindingFlags.NonPublic );
                    }

                    if ( _timescaleWhenPausedField != null )
                    {
                        _timescaleWhenPausedField.SetValue( pause, speed );
                        reply["timeScaleAfterUnpause"] = (float)_timescaleWhenPausedField.GetValue( pause );
                    }
                }

                return reply;
            }
            catch ( Exception ex )
            {
                Main.Log( $"SetGameSpeed error: {ex.Message}" );
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
        public static string GetCanonicalCampaignName( string campaignName )
        {
            if ( string.IsNullOrEmpty( campaignName ) )
            {
                return campaignName;
            }

            foreach ( var known in CampaignNames )
            {
                if ( string.Equals( known, campaignName, StringComparison.OrdinalIgnoreCase ) )
                {
                    return known;
                }
            }

            return campaignName;
        }

        public static string GetCampaignDisplayName( string campaignName )
        {
            if ( string.IsNullOrEmpty( campaignName ) )
            {
                return null;
            }

            return FormatCampaignDisplayName( GetCanonicalCampaignName( campaignName ) );
        }

        private static string FormatCampaignDisplayName( string campaignName )
        {
            return campaignName.Replace( "WM_", "" ).Replace( "(mouse)", "" ).Replace( "_", " " ).Trim();
        }

        public static object ListCampaigns()
        {
            try
            {
                var campaigns = new List<object>();
                for ( int i = 0; i < CampaignNames.Length; i++ )
                {
                    campaigns.Add( new
                    {
                        index = i,
                        name = CampaignNames[i],
                        displayName = FormatCampaignDisplayName( CampaignNames[i] )
                    } );
                }

                return new { success = true, campaigns = campaigns };
            }
            catch ( Exception ex )
            {
                Main.Log( $"ListCampaigns error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }

        public static object GoToLevel( int campaignIndex, int levelIndex )
        {
            try
            {
                if ( campaignIndex < 0 || campaignIndex >= CampaignNames.Length )
                {
                    return new
                    {
                        success = false,
                        error = $"Campaign index {campaignIndex} is out of range - valid indices are 0 to {CampaignNames.Length - 1} (see list_campaigns)"
                    };
                }

                if ( levelIndex < 0 )
                {
                    return new
                    {
                        success = false,
                        error = $"Level index {levelIndex} is out of range - level indices start at 0"
                    };
                }

                string[] suppressedBy = null;

                try
                {
                    MainThreadDispatcher.EnqueueAndWait( () =>
                    {
                        if ( !HeroController.IsPlayerPlaying( 0 ) )
                        {
                            PlayerProgress.currentWorldMapSaveSlot = 0;
                            GameState.Instance.currentWorldmapSave = PlayerProgress.Instance.saveSlots[0];

                            HeroController.PIDS[0] = PID.MyID;
                            HeroController.playerControllerIDs[0] = 0;
                            HeroController.SetPlayerName( 0, PlayerOptions.Instance.playerName );
                            HeroController.SetIsPlaying( 0, true );
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

                        GameModeController.LoadNextScene( GameState.Instance );

                        suppressedBy = GetActiveEnemySpawnSuppressors();
                    }, 3000 );
                }
                catch ( Exception ex )
                {
                    Main.Log( $"[GoToLevel] Failed to execute on main thread: {ex.Message}" );
                    throw;
                }

                var result = JObject.FromObject( new
                {
                    success = true,
                    message = $"Loading {CampaignNames[campaignIndex]} - Level {levelIndex + 1}"
                } );

                if ( suppressedBy != null && suppressedBy.Length > 0 )
                {
                    result["warning"] = $"Enemy spawning is suppressed by {string.Join( ", ", suppressedBy )}: enemies and bosses the level places are skipped, so a level that finishes on a boss cannot finish";
                }

                return result;
            }
            catch ( Exception ex )
            {
                Main.Log( $"GoToLevel error: {ex.Message}" );
                return new
                {
                    success = false,
                    error = ex.Message
                };
            }
        }

        #region Bro Swapping
        private const string SwapBrosRequired = "Swap Bros Mod is required but not active";

        private static JObject BroFailure( string error, string reason )
        {
            return JObject.FromObject( new
            {
                success = false,
                error,
                reason,
                availableBros = SwapBrosIntegration.GetAvailableBros().ToArray(),
                allNormal = SwapBrosIntegration.GetAllNormalBros().ToArray(),
                allExpendabros = SwapBrosIntegration.GetAllExpendabros().ToArray(),
                allUnfinished = SwapBrosIntegration.GetAllUnfinishedBros().ToArray()
            } );
        }

        private static JObject UnknownBro( string broName, out string known, out string origin )
        {
            known = SwapBrosIntegration.FindKnownBro( broName, out origin );
            if ( known != null )
            {
                return null;
            }

            string[] suggestions = MemberPath.ClosestNames( SwapBrosIntegration.AllKnownBros(), broName );
            string hint = suggestions.Length > 0 ? $" - did you mean {string.Join( ", ", suggestions.Select( name => $"'{name}'" ).ToArray() )}?" : "";
            JObject failure = BroFailure( $"Bro '{broName}' not found in any roster (vanilla, Expendabros, unfinished or BroMaker){hint}", "unknown-bro" );
            failure["suggestions"] = JArray.FromObject( suggestions );
            return failure;
        }

        public static object SwapBro( string broName, int playerNum, bool ignoreUnlocks = false )
        {
            try
            {
                if ( playerNum < Nums.PLAYER_MIN || playerNum >= Nums.PLAYER_MAX )
                {
                    return new { success = false, error = "Player number must be 0-3" };
                }

                if ( !SwapBrosIntegration.IsAvailable )
                {
                    return new { success = false, error = SwapBrosRequired, reason = "swap-bros-missing" };
                }

                object result = null;

                object ignoredUnlocks = null;

                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    string notPlaying = NotPlayingReason();
                    if ( notPlaying != null )
                    {
                        result = BroFailure( $"Cannot swap player {playerNum} to '{broName}': {notPlaying}", "no-level" );
                        return;
                    }

                    SlotState slot = ReadSlot( playerNum );
                    string refusal = SlotRefusal( slot, true );
                    if ( refusal != null )
                    {
                        result = BroFailure( $"Cannot swap player {playerNum} to '{broName}': {refusal}", SlotReasonCode( slot ) );
                        return;
                    }

                    string known;
                    string origin;
                    JObject unknown = UnknownBro( broName, out known, out origin );
                    if ( unknown != null )
                    {
                        result = unknown;
                        return;
                    }

                    Func<bool> swap = () =>
                    {
                        if ( !SwapBrosIntegration.IsInCurrentList( broName ) )
                        {
                            string why = SwapBrosIntegration.UnreachableReason( known, origin, !ignoreUnlocks );
                            result = BroFailure( $"Bro '{known}' is not in Swap Bros Mod's current list: {why}", "unreachable" );
                            return false;
                        }

                        bool swapped = SwapBrosIntegration.SwapToBroByName( playerNum, broName );
                        if ( !swapped )
                        {
                            result = BroFailure( $"Swap Bros Mod did not swap player {playerNum} to '{known}' - its log names any exception", "swap-failed" );
                            return false;
                        }

                        var player = HeroController.players[playerNum];
                        result = new
                        {
                            success = true,
                            broName = player?.character?.gameObject?.name ?? broName,
                            heroType = player?.character?.heroType.ToString() ?? "Unknown"
                        };
                        return true;
                    };

                    if ( ignoreUnlocks )
                    {
                        ignoredUnlocks = SwapBrosIntegration.RunIgnoringUnlocks( broName, swap );
                    }
                    else
                    {
                        swap();
                    }
                }, 5000 );

                if ( ignoredUnlocks == null )
                {
                    return result;
                }

                JObject reply = JObject.FromObject( result );
                reply["ignoredUnlocks"] = JObject.FromObject( ignoredUnlocks );
                return reply;
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
                    return new { success = false, error = SwapBrosRequired, reason = "swap-bros-missing" };
                }

                object result = null;

                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    string known;
                    string origin;
                    JObject unknown = UnknownBro( broName, out known, out origin );
                    if ( unknown != null )
                    {
                        result = unknown;
                        return;
                    }

                    if ( !SwapBrosIntegration.IsInCurrentList( broName ) )
                    {
                        string why = SwapBrosIntegration.UnreachableReason( known, origin, false );
                        result = BroFailure( $"Bro '{known}' is not in Swap Bros Mod's current list: {why}", "unreachable" );
                        return;
                    }

                    bool? alwaysChosenBefore = SwapBrosIntegration.ReadAlwaysChosen();
                    bool set = SwapBrosIntegration.SetNextBroByName( playerNum, broName );
                    if ( !set )
                    {
                        result = BroFailure( $"Swap Bros Mod did not select '{known}' for player {playerNum} - its log names any exception", "swap-failed" );
                        return;
                    }

                    // Swap Bros Mod's SetSelectedBroByName turns alwaysChosen on and saves nothing back.
                    result = new
                    {
                        success = true,
                        nextBro = broName,
                        alwaysChosen = new { before = alwaysChosenBefore, after = SwapBrosIntegration.ReadAlwaysChosen() }
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
                    return new { success = false, error = SwapBrosRequired, reason = "swap-bros-missing" };
                }

                object result = null;

                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    var available = SwapBrosIntegration.GetAvailableBros();
                    var normal = SwapBrosIntegration.GetAllNormalBros();
                    var expendabros = SwapBrosIntegration.GetAllExpendabros();
                    var unfinished = SwapBrosIntegration.GetAllUnfinishedBros();

                    var players = HeroController.players;
                    TestVanDammeAnim character = players != null && playerNum < players.Length ? players[playerNum]?.character : null;

                    var currentBros = new List<object>();
                    if ( players != null )
                    {
                        for ( int i = 0; i < players.Length; i++ )
                        {
                            if ( players[i]?.character == null )
                            {
                                continue;
                            }
                            currentBros.Add( new
                            {
                                playerNum = i,
                                currentBro = SwapBrosIntegration.GetLiveBroName( players[i].character ),
                                selectedBro = SwapBrosIntegration.GetSelectedBroName( i )
                            } );
                        }
                    }

                    result = new
                    {
                        success = true,
                        playerNum,
                        currentBro = SwapBrosIntegration.GetLiveBroName( character ),
                        selectedBro = SwapBrosIntegration.GetSelectedBroName( playerNum ),
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

                    string notPlaying = NotPlayingReason();
                    if ( notPlaying != null )
                    {
                        result = new { success = false, error = $"Cannot restart: {notPlaying}", scene = sceneName };
                        return;
                    }

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
