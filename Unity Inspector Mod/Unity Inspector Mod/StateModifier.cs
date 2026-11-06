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
        private const int NextFrameWaitMs = 1000;

        private sealed class ModifyOutcome
        {
            public object Failure;
            public int MainThreadId;
            public Component Component;
            public JObject Reply;
            public readonly List<string> Modified = new List<string>();
            public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
            public readonly Dictionary<string, object> ReadBack = new Dictionary<string, object>();
            public readonly List<object> Errors = new List<object>();
        }

        public static object ModifyComponent( string gameObjectPath, int? instanceId, string componentType, JObject properties )
        {
            try
            {
                ModifyOutcome outcome = null;

                // A struct link is read, written and stored back as one unit; off the main thread the
                // game can set a sibling field between the read and the store and lose it.
                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    outcome = ModifyComponentOnMainThread( gameObjectPath, instanceId, componentType, properties );
                }, 5000 );

                if ( outcome.Failure != null )
                {
                    return outcome.Failure;
                }

                if ( outcome.ReadBack.Count > 0 && outcome.MainThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId )
                {
                    List<object> reverted = CheckNextFrame( outcome );
                    outcome.Reply["nextFrameChecked"] = reverted != null;
                    if ( reverted != null && reverted.Count > 0 )
                    {
                        outcome.Reply["reverted"] = JToken.FromObject( reverted );
                    }
                }

                return outcome.Reply;
            }
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
            {
                Main.Log( $"ModifyComponent error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }

        private static ModifyOutcome ModifyComponentOnMainThread( string gameObjectPath, int? instanceId, string componentType, JObject properties )
        {
            var outcome = new ModifyOutcome { MainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId };

            if ( properties == null || properties.Count == 0 )
            {
                outcome.Failure = new { success = false, error = "No properties were given - pass at least one member name and value to write" };
                return outcome;
            }

            ResolvedGameObject target;
            try
            {
                target = GameObjectInspector.Resolve( gameObjectPath, instanceId );
            }
            catch ( ArgumentException ex )
            {
                outcome.Failure = new { success = false, error = ex.Message };
                return outcome;
            }

            GameObject go = target.GameObject;
            if ( GameObjectInspector.IsAsset( go ) )
            {
                outcome.Failure = new
                {
                    success = false,
                    reason = "asset",
                    error = $"{GameObjectInspector.GetPath( go )} is a prefab asset, not an object in a scene; writing it would change every copy the game spawns from it for the rest of its run"
                };
                return outcome;
            }

            Component component = GameObjectInspector.FindComponent( go, componentType );
            if ( component == null )
            {
                outcome.Failure = new { success = false, error = GameObjectInspector.ComponentNotFound( go, componentType ) };
                return outcome;
            }
            outcome.Component = component;

            foreach ( var prop in properties )
            {
                string propName = prop.Key;

                string reason;
                string[] suggestions;
                object written;
                string failure = SetPropertyValue( component, propName, prop.Value, out reason, out suggestions, out written );
                if ( failure != null )
                {
                    if ( suggestions != null )
                    {
                        outcome.Errors.Add( new { property = propName, reason, message = failure, suggestions } );
                    }
                    else
                    {
                        outcome.Errors.Add( new { property = propName, reason, message = failure } );
                    }
                    continue;
                }

                var read = GameObjectInspector.ReadMember( component, propName );
                if ( !read.Success )
                {
                    outcome.Modified.Add( propName );
                    outcome.Values[propName] = $"<not read back: {read.Error}>";
                    continue;
                }

                object serialized = GameObjectInspector.SerializeValue( read.Value );
                if ( !ValuesMatch( written, read.Value, propName ) )
                {
                    outcome.Errors.Add( new
                    {
                        property = propName,
                        reason = "not-kept",
                        message = $"{propName} was written but does not hold the value: the setter or the game changed it",
                        value = serialized
                    } );
                    continue;
                }

                outcome.Modified.Add( propName );
                outcome.Values[propName] = serialized;
                outcome.ReadBack[propName] = read.Value;
            }

            outcome.Reply = GameObjectInspector.WithLocation( target, new
            {
                success = outcome.Modified.Count > 0 || outcome.Errors.Count == 0,
                modifiedProperties = outcome.Modified,
                values = outcome.Values,
                errors = outcome.Errors,
                component = component.GetType().Name,
                gameObject = GameObjectInspector.GetPath( go )
            } );
            return outcome;
        }

        private static List<object> CheckNextFrame( ModifyOutcome outcome )
        {
            var done = new System.Threading.ManualResetEvent( false );
            List<object> reverted = null;

            try
            {
                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    MainThreadDispatcher.Instance.StartCoroutine( ReadAfterNextFrame( outcome.Component, outcome.ReadBack, found =>
                    {
                        reverted = found;
                        done.Set();
                    } ) );
                }, NextFrameWaitMs );
            }
            catch ( Exception )
            {
                return null;
            }

            return done.WaitOne( NextFrameWaitMs ) ? reverted : null;
        }

        private static System.Collections.IEnumerator ReadAfterNextFrame( Component component, Dictionary<string, object> written, Action<List<object>> report )
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            if ( component == null )
            {
                report( null );
                yield break;
            }

            var reverted = new List<object>();
            foreach ( var pair in written )
            {
                try
                {
                    var read = GameObjectInspector.ReadMember( component, pair.Key );
                    if ( !read.Success || ValuesMatch( pair.Value, read.Value, pair.Key ) )
                    {
                        continue;
                    }

                    reverted.Add( new
                    {
                        property = pair.Key,
                        value = GameObjectInspector.SerializeValue( read.Value ),
                        note = RevertNote( component, pair.Key )
                    } );
                }
                catch ( Exception )
                {
                }
            }
            report( reverted );
        }

        private static string RevertNote( Component component, string name )
        {
            string root = name.Split( HarmonyExtensions.PATH_SEPARATOR )[0];
            if ( component is Transform && ( root == "position" || root == "localPosition" ) && component.GetComponent<Unit>() != null )
            {
                return "the game set it back by the next frame: a unit's X and Y fields place it every frame - write those, or use teleport_player for a bro";
            }
            return "the game set it back by the next frame";
        }

        private static bool ValuesMatch( object written, object read, string member )
        {
            if ( written == null || read == null )
            {
                return MemberPath.IsNullReference( written ) && MemberPath.IsNullReference( read );
            }

            if ( written is Quaternion && read is Quaternion )
            {
                return SameRotation( (Quaternion)written, (Quaternion)read );
            }

            // Unity decomposes a rotation into angles of its own choosing: -90 reads back as 270.
            string leaf = member.Substring( member.LastIndexOf( HarmonyExtensions.PATH_SEPARATOR ) + 1 );
            if ( ( leaf == "eulerAngles" || leaf == "localEulerAngles" ) && written is Vector3 && read is Vector3 )
            {
                return SameRotation( Quaternion.Euler( (Vector3)written ), Quaternion.Euler( (Vector3)read ) );
            }

            return TokensMatch( Token( GameObjectInspector.SerializeValue( written ) ), Token( GameObjectInspector.SerializeValue( read ) ) );
        }

        private static bool SameRotation( Quaternion a, Quaternion b )
        {
            return Mathf.Abs( Quaternion.Dot( a, b ) ) > 1f - 1e-5f;
        }

        private static JToken Token( object serialized )
        {
            return serialized == null ? JValue.CreateNull() : JToken.FromObject( serialized );
        }

        private static bool TokensMatch( JToken a, JToken b )
        {
            bool aNumber = a.Type == JTokenType.Integer || a.Type == JTokenType.Float;
            bool bNumber = b.Type == JTokenType.Integer || b.Type == JTokenType.Float;
            if ( aNumber && bNumber )
            {
                double x = a.Value<double>();
                double y = b.Value<double>();
                return x == y || System.Math.Abs( x - y ) <= 1e-4 * System.Math.Max( 1.0, System.Math.Max( System.Math.Abs( x ), System.Math.Abs( y ) ) );
            }

            if ( a.Type != b.Type )
            {
                return false;
            }

            var objectA = a as JObject;
            if ( objectA != null )
            {
                var objectB = (JObject)b;
                if ( objectA.Count != objectB.Count )
                {
                    return false;
                }
                foreach ( var pair in objectA )
                {
                    JToken other;
                    if ( !objectB.TryGetValue( pair.Key, out other ) || !TokensMatch( pair.Value, other ) )
                    {
                        return false;
                    }
                }
                return true;
            }

            var arrayA = a as JArray;
            if ( arrayA != null )
            {
                var arrayB = (JArray)b;
                if ( arrayA.Count != arrayB.Count )
                {
                    return false;
                }
                for ( int i = 0; i < arrayA.Count; i++ )
                {
                    if ( !TokensMatch( arrayA[i], arrayB[i] ) )
                    {
                        return false;
                    }
                }
                return true;
            }

            return JToken.DeepEquals( a, b );
        }

        private static string SetPropertyValue( object target, string propertyName, JToken value, out string reason, out string[] suggestions, out object written )
        {
            reason = null;
            suggestions = null;
            written = null;

            if ( !MemberPath.IsPath( propertyName ) )
            {
                return SetMemberValue( target, propertyName, propertyName, value, out reason, out suggestions, out written );
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

                string asset = AssetInChain( resolution );
                if ( asset != null )
                {
                    reason = "asset";
                    return $"{propertyName} reaches {asset}, a prefab asset, not an object in a scene; writing it would change every copy the game spawns from it for the rest of its run";
                }

                // Refused before anything is written: a write past a boxed struct that cannot be
                // stored back would otherwise report success and change nothing.
                string blocked = MemberPath.CheckWriteBack( resolution, out reason );
                if ( blocked != null )
                {
                    return blocked;
                }

                string failure = SetMemberValue( resolution.Owner, resolution.Leaf, propertyName, value, out reason, out suggestions, out written );
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

        private static string AssetInChain( MemberPathResolution resolution )
        {
            foreach ( var step in resolution.Chain )
            {
                GameObject go = step.Value as GameObject;
                var component = step.Value as Component;
                if ( component != null )
                {
                    go = component.gameObject;
                }
                if ( go != null && GameObjectInspector.IsAsset( go ) )
                {
                    return GameObjectInspector.GetPath( go );
                }
            }
            return null;
        }

        private static string SetMemberValue( object target, string memberName, string reportedName, JToken value, out string reason, out string[] suggestions, out object written )
        {
            const BindingFlags memberFlags = BindingFlags.DeclaredOnly | BindingFlags.Public
                                                                       | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            reason = null;
            suggestions = null;
            written = null;

            try
            {
                Type type = target.GetType();

                int index;
                string indexError;
                if ( MemberPath.TryIndex( target, memberName, reportedName, out index, out indexError ) )
                {
                    if ( indexError != null )
                    {
                        reason = "not-found";
                        return indexError;
                    }

                    var list = (System.Collections.IList)target;
                    if ( list.IsReadOnly )
                    {
                        reason = "read-only";
                        return $"The {type.Name} holding {reportedName} is read-only";
                    }

                    written = ConvertValue( value, MemberPath.ElementType( type ), () => list[index], reportedName );
                    list[index] = written;
                    return null;
                }

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

                        written = ConvertValue( value, field.FieldType, () => MemberPath.ReadField( field, target ), reportedName );
                        field.SetValue( field.IsStatic ? null : target, written );
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
                        MethodInfo getter = property.GetGetMethod( true );
                        bool readable = getter != null && GameObjectInspector.SharedTwin( property ) == null;
                        written = ConvertValue( value, property.PropertyType,
                            readable ? (Func<object>)( () => property.GetValue( getter.IsStatic ? null : target, null ) ) : null, reportedName );
                        property.SetValue( setter != null && setter.IsStatic ? null : target, written, null );
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

        private static string Sent( JToken value )
        {
            string text = value.ToString( Newtonsoft.Json.Formatting.None );
            return text.Length > 100 ? text.Substring( 0, 100 ) + "..." : text;
        }

        private static ArgumentException Refused( string member, Type type, JToken value )
        {
            return new ArgumentException( $"{member} takes a {GameObjectInspector.TypeName( type )}, got {Sent( value )}" );
        }

        private static double FiniteNumber( JToken value, string member, Type type )
        {
            if ( value.Type != JTokenType.Integer && value.Type != JTokenType.Float )
            {
                throw Refused( member, type, value );
            }

            double number = value.Value<double>();
            if ( double.IsNaN( number ) || double.IsInfinity( number ) || ( type == typeof( float ) && float.IsInfinity( (float)number ) ) )
            {
                throw new ArgumentException( $"{member} takes a finite {GameObjectInspector.TypeName( type )}, got {Sent( value )}" );
            }
            return number;
        }

        private static string[] StructKeys( Type type )
        {
            if ( type == typeof( Vector2 ) )
            {
                return new[] { "x", "y" };
            }
            if ( type == typeof( Vector3 ) )
            {
                return new[] { "x", "y", "z" };
            }
            if ( type == typeof( Vector4 ) )
            {
                return new[] { "x", "y", "z", "w" };
            }
            if ( type == typeof( Color ) )
            {
                return new[] { "r", "g", "b", "a" };
            }
            if ( type == typeof( Rect ) )
            {
                return new[] { "x", "y", "width", "height" };
            }
            if ( type == typeof( Quaternion ) )
            {
                return new[] { "x", "y", "z", "w", "euler" };
            }
            return null;
        }

        private static void CheckKeys( JToken value, string member, string[] keys )
        {
            var unknown = ( (JObject)value ).Properties().Select( p => p.Name ).Where( name => Array.IndexOf( keys, name ) < 0 ).ToArray();
            if ( unknown.Length > 0 )
            {
                throw new ArgumentException( $"{member} takes the keys {string.Join( ", ", keys )}; got {string.Join( ", ", unknown )}" );
            }
        }

        private static float Part( JToken value, string key, float current, string member )
        {
            var part = value[key];
            if ( part == null || part.Type == JTokenType.Null )
            {
                return current;
            }
            return (float)FiniteNumber( part, member + "." + key, typeof( float ) );
        }

        private static object ConvertValue( JToken value, Type targetType, Func<object> current, string member )
        {
            if ( value == null || value.Type == JTokenType.Null )
            {
                return targetType.IsValueType && Nullable.GetUnderlyingType( targetType ) == null ? Activator.CreateInstance( targetType ) : null;
            }

            Type type = Nullable.GetUnderlyingType( targetType ) ?? targetType;

            if ( value.Type == JTokenType.Object )
            {
                string[] keys = StructKeys( type );
                if ( keys != null )
                {
                    CheckKeys( value, member, keys );
                    if ( type == typeof( Quaternion ) && value["euler"] != null )
                    {
                        if ( ( (JObject)value ).Count != 1 )
                        {
                            throw new ArgumentException( $"{member} takes either euler or x, y, z and w, not both" );
                        }
                        if ( value["euler"].Type != JTokenType.Object )
                        {
                            throw new ArgumentException( $"{member}.euler takes an object with x, y and z, got {Sent( value["euler"] )}" );
                        }
                        CheckKeys( value["euler"], member + ".euler", new[] { "x", "y", "z" } );
                    }
                }

                object now = null;
                if ( current != null && ( type == typeof( Vector2 ) || type == typeof( Vector3 ) || type == typeof( Vector4 )
                                          || type == typeof( Color ) || type == typeof( Quaternion ) || type == typeof( Rect ) ) )
                {
                    now = current();
                }

                if ( type == typeof( Vector3 ) )
                {
                    var v = now is Vector3 ? (Vector3)now : Vector3.zero;
                    return new Vector3( Part( value, "x", v.x, member ), Part( value, "y", v.y, member ), Part( value, "z", v.z, member ) );
                }
                if ( type == typeof( Vector2 ) )
                {
                    var v = now is Vector2 ? (Vector2)now : Vector2.zero;
                    return new Vector2( Part( value, "x", v.x, member ), Part( value, "y", v.y, member ) );
                }
                if ( type == typeof( Vector4 ) )
                {
                    var v = now is Vector4 ? (Vector4)now : Vector4.zero;
                    return new Vector4( Part( value, "x", v.x, member ), Part( value, "y", v.y, member ), Part( value, "z", v.z, member ), Part( value, "w", v.w, member ) );
                }
                if ( type == typeof( Color ) )
                {
                    var c = now is Color ? (Color)now : new Color( 0f, 0f, 0f, 1f );
                    return new Color( Part( value, "r", c.r, member ), Part( value, "g", c.g, member ), Part( value, "b", c.b, member ), Part( value, "a", c.a, member ) );
                }
                if ( type == typeof( Rect ) )
                {
                    var r = now is Rect ? (Rect)now : new Rect();
                    return new Rect( Part( value, "x", r.x, member ), Part( value, "y", r.y, member ), Part( value, "width", r.width, member ), Part( value, "height", r.height, member ) );
                }
                if ( type == typeof( Quaternion ) )
                {
                    var q = now is Quaternion ? (Quaternion)now : Quaternion.identity;
                    var euler = value["euler"];
                    if ( euler != null && euler.Type == JTokenType.Object )
                    {
                        var e = q.eulerAngles;
                        return Quaternion.Euler( Part( euler, "x", e.x, member + ".euler" ), Part( euler, "y", e.y, member + ".euler" ), Part( euler, "z", e.z, member + ".euler" ) );
                    }

                    float x = Part( value, "x", q.x, member ), y = Part( value, "y", q.y, member ), z = Part( value, "z", q.z, member ), w = Part( value, "w", q.w, member );
                    float length = Mathf.Sqrt( x * x + y * y + z * z + w * w );
                    if ( length < 1e-6f )
                    {
                        throw new ArgumentException( $"{member}: a quaternion of all zeroes is not a rotation" );
                    }
                    return new Quaternion( x / length, y / length, z / length, w / length );
                }
            }

            if ( type.IsEnum )
            {
                if ( value.Type == JTokenType.String )
                {
                    try
                    {
                        return Enum.Parse( type, (string)value );
                    }
                    catch ( ArgumentException )
                    {
                        throw new ArgumentException( $"{member} takes a {type.Name}: one of {string.Join( ", ", Enum.GetNames( type ) )}; got {Sent( value )}" );
                    }
                }
                if ( value.Type == JTokenType.Integer )
                {
                    return Enum.ToObject( type, value.Value<long>() );
                }
                throw Refused( member, type, value );
            }

            if ( type.IsPrimitive && type != typeof( bool ) && type != typeof( char ) && type != typeof( IntPtr ) && type != typeof( UIntPtr ) )
            {
                double number = FiniteNumber( value, member, type );
                if ( IsIntegral( type ) && number != System.Math.Floor( number ) )
                {
                    throw new ArgumentException( $"{member} takes a whole number ({type.Name}), got {Sent( value )}" );
                }
                try
                {
                    return value.ToObject( targetType );
                }
                catch ( OverflowException )
                {
                    throw new ArgumentException( $"{member} is out of range for a {type.Name}, got {Sent( value )}" );
                }
            }

            if ( type == typeof( bool ) && value.Type != JTokenType.Boolean )
            {
                throw Refused( member, type, value );
            }

            if ( type == typeof( string ) && value.Type != JTokenType.String )
            {
                throw Refused( member, type, value );
            }

            try
            {
                return value.ToObject( targetType );
            }
            catch ( Exception ex )
            {
                throw new ArgumentException( $"{member} takes a {GameObjectInspector.TypeName( targetType )}, got {Sent( value )}: {MemberPath.Unwrap( ex ).Message}" );
            }
        }

        private static bool IsIntegral( Type type )
        {
            type = Nullable.GetUnderlyingType( type ) ?? type;
            return type == typeof( int ) || type == typeof( long ) || type == typeof( short ) || type == typeof( byte )
                   || type == typeof( uint ) || type == typeof( ulong ) || type == typeof( ushort ) || type == typeof( sbyte );
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
            if ( GameModeController.IsLevelFinished() )
            {
                return "no level is being played - the level has finished";
            }
            if ( SwitchingScenes() )
            {
                return "no level is being played - the game is switching to the next scene";
            }
            if ( FadingToAnotherScene() )
            {
                return "no level is being played - the screen is fading to another scene";
            }
            return null;
        }

        private static FieldInfo _gameModeControllerField;

        private static bool SwitchingScenes()
        {
            if ( _gameModeControllerField == null )
            {
                _gameModeControllerField = typeof( GameModeController ).GetField( "instance", BindingFlags.Static | BindingFlags.NonPublic );
            }

            GameModeController controller = _gameModeControllerField == null ? null : _gameModeControllerField.GetValue( null ) as GameModeController;
            return controller != null && GameModeController.InSwitchingScenesPhase();
        }

        private static bool FadingToAnotherScene()
        {
            Fader fader = Fader.instance;
            return fader != null && ( fader.fading || fader.fadingBlack || fader.fadingHorizontalSwipe );
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
                slot.State = players[index].firstDeployment ? "joined" : "respawning";
                return slot;
            }

            slot.Character = character;
            if ( character.pilottedUnit != null )
            {
                slot.Piloting = VehicleName( character.pilottedUnit );
            }

            if ( !CharacterIsAlive( character ) )
            {
                slot.State = "dead";
            }
            else if ( slot.Piloting == null && ( character.isOnHelicopter || character.HidingPlayer ) )
            {
                slot.State = InOpeningTransport( character ) ? "arriving" : "held";
            }
            else
            {
                slot.State = "alive";
            }

            return slot;
        }

        private const BindingFlags ContainedBrosFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo TruckBros = typeof( HeroTransport ).GetField( "containedBros", ContainedBrosFlags );
        private static readonly FieldInfo HelicopterBros = typeof( Helicopter ).GetField( "containedBros", ContainedBrosFlags );
        private static readonly FieldInfo PortalBros = typeof( HeroLevelEnterPortal ).GetField( "containedBros", ContainedBrosFlags );

        private static bool InOpeningTransport( TestVanDammeAnim character )
        {
            return Holds( TruckBros, HeroTransport.instance, character )
                   || Holds( HelicopterBros, Helicopter.DropOffHeliInstance, character )
                   || Holds( PortalBros, HeroLevelEnterPortal.instance, character );
        }

        private static bool Holds( FieldInfo contained, UnityEngine.Object transport, TestVanDammeAnim character )
        {
            if ( contained == null || transport == null )
            {
                return false;
            }
            var bros = contained.GetValue( transport ) as List<TestVanDammeAnim>;
            return bros != null && bros.Contains( character );
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
                case "arriving": return $"player {slot.Index}'s bro has not been released into the level yet";
                case "held": return $"player {slot.Index}'s bro is held by the game (on a helicopter or hidden)";
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
                case "arriving": return "not-released";
                case "held": return "held";
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
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
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

            // Map.IsWithinBounds passes a huge or infinite x or y, since its int column cast overflows; NaN fails these comparisons.
            if ( !( x >= 0f && x < MapMaxX() && y >= 0f && y < MapMaxY() ) )
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
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
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

            if ( character.willComeBackToLife )
            {
                error = $"player {slot.Index} was not killed: it carries a revive halo and would come back to life";
                return false;
            }

            // The hit itself clears some damage absorbers (a parachute closes).
            string absorber = DamageAbsorber( character );

            character.Damage( character.health, DamageType.Crush, 0f, 0f, 0, null, character.X, character.Y );

            if ( character.WillReviveAlready )
            {
                error = $"player {slot.Index} died but is reviving: the game brings the bro back to life";
                return false;
            }

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
                        LogSettingMissOnce( missing );
                        suppressionNote = $"{missing} - whether it suppresses enemy spawns could not be read, so the spawn ran unchanged";
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
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
            {
                Main.Log( $"SpawnEntity error: {ex.Message}" );
                return new { success = false, error = ex.Message, stackTrace = ex.StackTrace };
            }
        }

        private static object SpawnEntityOnMainThread( string entityType, float x, float y, float xI, float yI )
        {
            string notPlaying = NotPlayingReason();
            if ( notPlaying != null )
            {
                return new { success = false, error = $"Cannot spawn: {notPlaying}", reason = "no-level" };
            }

            Map map = Map.Instance;

            if ( map == null || map.activeTheme == null )
            {
                return new { success = false, error = "Cannot spawn: the level has no theme loaded", reason = "no-level" };
            }

            if ( !IsFinite( x ) || !IsFinite( y ) || !IsFinite( xI ) || !IsFinite( yI ) )
            {
                return new { success = false, error = $"Cannot spawn: x, y, xI and yI must be finite numbers, got ({x}, {y}) moving ({xI}, {yI})" };
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

        private static bool IsFinite( float value )
        {
            return !float.IsNaN( value ) && !float.IsInfinity( value );
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
                    LogSettingMissOnce( reason );
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
        private static readonly HashSet<string> loggedSettingMisses = new HashSet<string>();

        private static void LogSettingMissOnce( string reason )
        {
            if ( reason == null || reason == UtilityModNotLoaded || !loggedSettingMisses.Add( reason ) )
            {
                return;
            }
            Main.Log( $"{reason} - cannot tell whether enemy spawning is suppressed" );
        }

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
            if ( !( enabled is bool ) )
            {
                reason = "Utility Mod is loaded but its enabled field is missing";
                return null;
            }

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
                Enabled = (bool)enabled,
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

                JObject reply = null;
                float expected = speed;
                int writeFrame = 0;

                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    reply = JObject.FromObject( new { success = true } );
                    writeFrame = Time.frameCount;

                    // The offline pause menu holds timeScale at 1 and restores the value it saved when it closes.
                    PauseController pause = PauseController.instance;
                    FieldInfo pausedScale = TimescaleWhenPausedField();
                    if ( PauseController.pauseStatus == PauseStatus.MenuPause && Connect.IsOffline && pause != null && pausedScale != null )
                    {
                        pausedScale.SetValue( pause, speed );
                        expected = Time.timeScale;
                        reply["timeScaleAfterUnpause"] = (float)pausedScale.GetValue( pause );
                    }
                    else
                    {
                        Time.timeScale = speed;
                    }

                    string effect = GameTimeEffect();
                    if ( effect != null )
                    {
                        reply["gameTimeEffect"] = effect;
                    }
                }, 5000 );

                float settled = expected;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                // TimeController and HeroController rewrite timeScale in their own Update, which can run before or after the write's frame.
                bool readBack = false;
                while ( !readBack && watch.ElapsedMilliseconds < SpeedSettleMs )
                {
                    System.Threading.Thread.Sleep( 20 );
                    int frame = 0;
                    try
                    {
                        MainThreadDispatcher.EnqueueAndWait( () =>
                        {
                            frame = Time.frameCount;
                            settled = Time.timeScale;
                        }, 2000 );
                    }
                    catch ( TimeoutException )
                    {
                        continue;
                    }
                    readBack = frame >= writeFrame + 2;
                }

                reply["timeScale"] = settled;
                if ( !readBack )
                {
                    reply["success"] = false;
                    reply["mainThreadAnswered"] = false;
                    reply["error"] = $"timeScale was written, but no later frame ran within {SpeedSettleMs}ms to read it back - the main thread is busy";
                }
                else if ( settled != expected )
                {
                    reply["success"] = false;
                    reply["error"] = $"The game's own time effect set timeScale to {settled} after the write of {expected}"
                                     + ( reply["gameTimeEffect"] != null ? $" ({reply["gameTimeEffect"]})" : "" );
                }

                return reply;
            }
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
            {
                Main.Log( $"SetGameSpeed error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }

        public const int SpeedSettleMs = 1000;

        private static FieldInfo TimescaleWhenPausedField()
        {
            if ( _timescaleWhenPausedField == null )
            {
                _timescaleWhenPausedField = typeof( PauseController ).GetField( "timescaleWhenPaused", BindingFlags.Instance | BindingFlags.NonPublic );
            }
            return _timescaleWhenPausedField;
        }

        private static FieldInfo _timeStoppedField;
        private static FieldInfo _timeBoostTimeField;

        private static string GameTimeEffect()
        {
            if ( _timeStoppedField == null )
            {
                _timeStoppedField = typeof( TimeController ).GetField( "timeStopped", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public );
            }
            if ( _timeBoostTimeField == null )
            {
                _timeBoostTimeField = typeof( HeroController ).GetField( "timeBoostTime", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public );
            }

            if ( _timeStoppedField != null && (bool)_timeStoppedField.GetValue( null ) )
            {
                return "the game's slow-motion is running and sets timeScale back to 1 when it ends";
            }
            if ( _timeBoostTimeField != null && (float)_timeBoostTimeField.GetValue( null ) > 0f )
            {
                return "a hero time boost is running and sets timeScale back to 1 when it ends";
            }
            return null;
        }

        // Each is a campaign TextAsset in StreamingAssets/levels.assetbundle; the game cannot load any other name.
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
            "Challenge_Ammo",
            "Challenge_Dash",
            "Challenge_Mech1",
            "Challenge_MacBrover",
            "Challenge_TimeBro",
            "MuscleTemple_1",
            "MuscleTemple_2",
            "MuscleTemple_3",
            "MuscleTemple_4"
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
            int fromMap;
            return GoToLevel( campaignIndex, levelIndex, out fromMap );
        }

        private static object GoToLevel( int campaignIndex, int levelIndex, out int fromMap )
        {
            fromMap = 0;
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
                string refusal = null;
                int? saveSlot = null;
                int loadedFrom = 0;

                try
                {
                    MainThreadDispatcher.EnqueueAndWait( () =>
                    {
                        bool joinPlayer = !HeroController.IsPlayerPlaying( 0 );
                        if ( joinPlayer || GameState.Instance.currentWorldmapSave == null )
                        {
                            int slot = UsableSaveSlot( out refusal );
                            if ( refusal != null )
                            {
                                return;
                            }

                            PlayerProgress.currentWorldMapSaveSlot = slot;
                            GameState.Instance.currentWorldmapSave = PlayerProgress.Instance.saveSlots[slot];
                            saveSlot = slot;
                        }

                        if ( joinPlayer )
                        {
                            HeroController.PIDS[0] = PID.MyID;
                            HeroController.playerControllerIDs[0] = 0;
                            HeroController.SetPlayerName( 0, PlayerOptions.Instance.playerName );
                            HeroController.SetIsPlaying( 0, true );
                        }

                        Map current = Map.Instance;
                        loadedFrom = current != null ? current.GetInstanceID() : 0;
                        ReleasePause();

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

                if ( refusal != null )
                {
                    return new { success = false, error = $"Cannot load {CampaignNames[campaignIndex]}: {refusal}", reason = "no-save-slot" };
                }

                var result = JObject.FromObject( new
                {
                    success = true,
                    message = $"Loading {CampaignNames[campaignIndex]} - Level {levelIndex + 1}"
                } );

                fromMap = loadedFrom;
                if ( saveSlot.HasValue )
                {
                    result["saveSlot"] = saveSlot.Value;
                }

                if ( suppressedBy != null && suppressedBy.Length > 0 )
                {
                    result["warning"] = $"Enemy spawning is suppressed by {string.Join( ", ", suppressedBy )}: enemies and bosses the level places are skipped, so a level that finishes on a boss cannot finish";
                }

                return result;
            }
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
            {
                Main.Log( $"GoToLevel error: {ex.Message}" );
                return new
                {
                    success = false,
                    error = ex.Message
                };
            }
        }

        public static object GoToLevelAndWait( int campaignIndex, int levelIndex )
        {
            int fromMap;
            object requested = GoToLevel( campaignIndex, levelIndex, out fromMap );
            JObject reply = requested as JObject;
            if ( reply == null || reply["success"]?.Value<bool>() != true )
            {
                return requested;
            }

            string campaign = CampaignNames[campaignIndex];
            string pending = AwaitLevelInPlay( () => LevelPending( fromMap, campaign, levelIndex ), reply );

            if ( pending != null )
            {
                reply["success"] = false;
                reply["error"] = $"{campaign} level {levelIndex + 1} was requested but was not in play after {LevelArrivalBudgetMs / 1000}s: {pending}";
                reply["reason"] = "not-loaded";
                reply.Remove( "message" );
                return reply;
            }

            reply["message"] = $"Loaded {campaign} - Level {levelIndex + 1}";
            return reply;
        }

        public const int LevelArrivalBudgetMs = 60000;
        public const int LevelArrivalPollMs = 250;
        public const int LevelArrivalCheckMs = 1000;

        private static string AwaitLevelInPlay( Func<string> pending, JObject reply )
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            string last = "the main thread never answered";
            bool answered = false;

            while ( true )
            {
                System.Threading.Thread.Sleep( LevelArrivalPollMs );
                string observed = null;
                PauseStatus pause = PauseStatus.UnPaused;
                try
                {
                    MainThreadDispatcher.EnqueueAndWait( () =>
                    {
                        observed = pending();
                        pause = PauseController.pauseStatus;
                    }, LevelArrivalCheckMs );
                    answered = true;
                    if ( observed == null )
                    {
                        reply["loaded"] = true;
                        reply["seconds"] = System.Math.Round( watch.Elapsed.TotalSeconds, 1 );
                        if ( pause == PauseStatus.CutscenePause )
                        {
                            reply["cutscene"] = pause.ToString();
                            string held = "A cutscene holds the game paused (CutscenePause) until a key is pressed, and no bro moves until it ends: simulate_input with action start skips it";
                            string earlier = reply["warning"]?.Value<string>();
                            reply["warning"] = string.IsNullOrEmpty( earlier ) ? held : $"{earlier}. {held}";
                        }
                        return null;
                    }
                    last = observed;
                }
                catch ( TimeoutException )
                {
                    answered = false;
                    last = "the main thread was busy, as it is while a scene loads";
                }
                catch ( MainThreadDispatcher.MainThreadActionRunningException )
                {
                    answered = false;
                    last = "the main thread is not advancing";
                }

                if ( watch.ElapsedMilliseconds >= LevelArrivalBudgetMs )
                {
                    reply["loaded"] = false;
                    reply["scene"] = SceneManager.GetActiveScene().name;
                    if ( !answered )
                    {
                        reply["mainThreadAnswered"] = false;
                    }
                    return last;
                }
            }
        }

        private static string LevelPending( int fromMap, string campaign, int levelIndex )
        {
            string notPlaying = NotPlayingReason();
            if ( notPlaying != null )
            {
                return notPlaying;
            }

            Map map = Map.Instance;
            if ( map == null || map.GetInstanceID() == fromMap )
            {
                return "the previous level is still the one loaded";
            }

            if ( campaign != null )
            {
                if ( LevelSelectionController.CurrentLevelNum != levelIndex )
                {
                    return $"level index {LevelSelectionController.CurrentLevelNum} is loaded instead";
                }

                Campaign loaded = LevelSelectionController.currentCampaign;
                // Past the campaign's last level the game loads the Victory map instead.
                if ( loaded != null && levelIndex < loaded.Length && !string.Equals( loaded.name, campaign, StringComparison.OrdinalIgnoreCase ) )
                {
                    return $"campaign {loaded.name} is loaded instead";
                }
            }

            // A bro's intro holds the game until a key, his transport frozen with him still on it.
            if ( PauseController.pauseStatus == PauseStatus.CutscenePause )
            {
                return null;
            }

            for ( int i = 0; i < Nums.PLAYER_MAX; i++ )
            {
                if ( ReadSlot( i ).State == "alive" )
                {
                    return null;
                }
            }

            return "no player's bro has been released into the level yet";
        }

        // SaveSlotsMenu opens on max(currentWorldMapSaveSlot, 0).
        private static int UsableSaveSlot( out string refusal )
        {
            refusal = null;
            WorldMapProgressSaveSlot[] slots = PlayerProgress.Instance != null ? PlayerProgress.Instance.saveSlots : null;
            if ( slots == null || slots.Length == 0 )
            {
                refusal = "the game has no save slots loaded";
                return -1;
            }

            int preferred = Mathf.Clamp( PlayerProgress.currentWorldMapSaveSlot, 0, slots.Length - 1 );
            if ( SaveSlotUsable( slots[preferred] ) )
            {
                return preferred;
            }

            for ( int i = 0; i < slots.Length; i++ )
            {
                if ( SaveSlotUsable( slots[i] ) )
                {
                    return i;
                }
            }

            refusal = "no save slot holds a campaign that is not an IronBro run - start one from the game's menu first";
            return -1;
        }

        private static bool SaveSlotUsable( WorldMapProgressSaveSlot slot )
        {
            return slot != null && !slot.worldMapIsHardcoreCampaign;
        }

        private static MethodInfo _unpauseGameObjects;

        // A load from any pause would leave the objects the pause deactivated, persistent ones included, inactive for good.
        private static void ReleasePause()
        {
            // CutsceneController.cutSceneInProgress is static and outlives the scene.
            if ( CutsceneController.InCutscene && CutsceneController.Instance != null )
            {
                CutsceneController.Instance.CloseAllCutscenes();
            }

            if ( PauseController.pauseStatus == PauseStatus.MenuPause )
            {
                PauseController.SetPause( PauseStatus.UnPaused );
            }

            if ( PauseController.pauseStatus == PauseStatus.UnPaused )
            {
                return;
            }

            // TogglePause leaves a menu pause in place while a submenu is open, and handles no other pause.
            if ( _unpauseGameObjects == null )
            {
                _unpauseGameObjects = typeof( PauseController ).GetMethod( "UnpauseGameObjects", BindingFlags.Static | BindingFlags.NonPublic );
            }

            if ( _unpauseGameObjects != null )
            {
                _unpauseGameObjects.Invoke( null, null );
            }
            PauseController.pauseStatus = PauseStatus.UnPaused;
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
                    return new { success = false, error = "Player number must be 0-3", reason = "bad-player" };
                }

                if ( !SwapBrosIntegration.IsAvailable )
                {
                    return new { success = false, error = SwapBrosRequired, reason = "swap-bros-missing" };
                }

                object result = null;
                object ignoredUnlocks = null;
                string landedBro = null;
                Player landedPlayer = null;
                int landedCharacter = 0;
                int landedMap = 0;

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
                            string why = SwapBrosIntegration.UnreachableReason( known, origin, ignoreUnlocks, !ignoreUnlocks );
                            result = BroFailure( $"Bro '{known}' is not in Swap Bros Mod's current list: {why}", "unreachable" );
                            return false;
                        }

                        bool swapped = SwapKeepingUnlocks( playerNum, broName );
                        if ( !swapped )
                        {
                            result = BroFailure( $"Swap Bros Mod did not swap player {playerNum} to '{known}' - its log names any exception", "swap-failed" );
                            return false;
                        }

                        Player player = HeroController.players[playerNum];
                        TestVanDammeAnim character = player != null ? player.character : null;
                        string missed = SwapMiss( character, known );
                        if ( missed != null )
                        {
                            JObject failure = BroFailure( $"Swap Bros Mod reported swapping player {playerNum} to '{known}', but {missed}", "swap-failed" );
                            failure["currentBro"] = SwapBrosIntegration.GetLiveBroName( character );
                            result = failure;
                            return false;
                        }

                        JObject swappedReply = JObject.FromObject( new
                        {
                            success = true,
                            broName = SwapBrosIntegration.GetLiveBroName( character ),
                            objectName = character.gameObject.name
                        } );
                        // A BroMaker bro's heroType is its preset's base type, not the bro.
                        if ( !SwapBrosIntegration.IsCustomBro( character ) )
                        {
                            swappedReply["heroType"] = character.heroType.ToString();
                        }

                        result = swappedReply;
                        landedBro = known;
                        landedPlayer = player;
                        landedCharacter = character.GetInstanceID();
                        landedMap = Map.Instance != null ? Map.Instance.GetInstanceID() : 0;
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

                JObject reply = JObject.FromObject( result );
                if ( ignoredUnlocks != null )
                {
                    reply["ignoredUnlocks"] = JObject.FromObject( ignoredUnlocks );
                }

                if ( landedPlayer == null )
                {
                    return reply;
                }

                System.Threading.Thread.Sleep( SwapConfirmDelayMs );

                string undone = null;
                string currentBro = null;
                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    Player now = HeroController.players[playerNum];
                    TestVanDammeAnim character = now != null ? now.character : null;
                    currentBro = SwapBrosIntegration.GetLiveBroName( character );

                    if ( Map.Instance == null || Map.Instance.GetInstanceID() != landedMap )
                    {
                        undone = "the level was replaced";
                    }
                    else if ( now != landedPlayer )
                    {
                        undone = "the slot's Player was replaced";
                    }
                    else if ( character == null )
                    {
                        undone = "the slot has no bro";
                    }
                    else if ( character.GetInstanceID() != landedCharacter )
                    {
                        undone = $"the slot now holds {currentBro}";
                    }
                    else if ( !CharacterIsAlive( character ) )
                    {
                        undone = "the swapped-in bro died";
                    }
                }, SwapConfirmBudgetMs );

                if ( undone != null )
                {
                    reply["success"] = false;
                    reply["error"] = $"Player {playerNum} was swapped to '{landedBro}', but {undone} within {SwapConfirmDelayMs}ms";
                    reply["reason"] = "swap-undone";
                    reply["currentBro"] = currentBro;
                }

                return reply;
            }
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
            {
                Main.Log( $"SwapBro error: {ex.Message}" );
                return new { success = false, error = ex.GetBaseException().Message, reason = BroErrorReason( ex ) };
            }
        }

        public const int SwapConfirmDelayMs = 500;
        public const int SwapConfirmBudgetMs = 2000;

        private static string BroErrorReason( Exception ex )
        {
            if ( ex is SwapBrosIntegration.IncompatibleException )
            {
                return "swap-bros-incompatible";
            }
            return "error";
        }

        private static string SwapMiss( TestVanDammeAnim character, string known )
        {
            if ( character == null )
            {
                return "the slot holds no bro";
            }
            if ( !character.gameObject.activeInHierarchy )
            {
                return "the slot's bro is inactive";
            }
            if ( !character.enabled || character.isOnHelicopter || character.HidingPlayer )
            {
                return "the slot's bro is held by the game";
            }
            if ( !CharacterIsAlive( character ) )
            {
                return "the slot's bro is dead";
            }

            string live = SwapBrosIntegration.GetLiveBroName( character );
            if ( !string.Equals( live, known, StringComparison.OrdinalIgnoreCase ) )
            {
                return $"the slot holds {live}";
            }
            return null;
        }

        // Player.SpawnHero adds a campaign hero to the save's unlocks and plays the unlock cutscene of one never played.
        private static bool SwapKeepingUnlocks( int playerNum, string broName )
        {
            PlayerProgress progress = PlayerProgress.Instance;
            List<HeroType> unlocked = progress != null && progress.unlockedHeroes != null ? new List<HeroType>( progress.unlockedHeroes ) : null;
            List<HeroType> unplayed = progress != null && progress.yetToBePlayedUnlockedHeroes != null ? new List<HeroType>( progress.yetToBePlayedUnlockedHeroes ) : null;

            if ( unplayed != null )
            {
                progress.yetToBePlayedUnlockedHeroes.Clear();
            }

            try
            {
                return SwapBrosIntegration.SwapToBroByName( playerNum, broName );
            }
            finally
            {
                if ( unlocked != null )
                {
                    progress.unlockedHeroes.Clear();
                    progress.unlockedHeroes.AddRange( unlocked );
                }
                if ( unplayed != null )
                {
                    progress.yetToBePlayedUnlockedHeroes.Clear();
                    progress.yetToBePlayedUnlockedHeroes.AddRange( unplayed );
                }
            }
        }

        public static object SetBro( string broName, int playerNum )
        {
            try
            {
                if ( playerNum < Nums.PLAYER_MIN || playerNum >= Nums.PLAYER_MAX )
                {
                    return new { success = false, error = "Player number must be 0-3", reason = "bad-player" };
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
                        string why = SwapBrosIntegration.UnreachableReason( known, origin, false, false );
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

                    string selected = SwapBrosIntegration.GetSelectedBroName( playerNum );
                    if ( !string.Equals( selected, known, StringComparison.OrdinalIgnoreCase ) )
                    {
                        JObject failure = BroFailure( $"Swap Bros Mod accepted '{known}' for player {playerNum}, but its selection reads {selected ?? "nothing"}", "swap-failed" );
                        failure["selectedBro"] = selected;
                        result = failure;
                        return;
                    }

                    // Swap Bros Mod's SetSelectedBroByName turns alwaysChosen on and saves nothing back.
                    JObject reply = JObject.FromObject( new
                    {
                        success = true,
                        nextBro = known,
                        alwaysChosen = new { before = alwaysChosenBefore, after = SwapBrosIntegration.ReadAlwaysChosen() }
                    } );

                    string overridden = SwapBrosIntegration.NextSpawnOverride( playerNum );
                    reply["appliesThisLevel"] = overridden == null;
                    if ( overridden != null )
                    {
                        reply["note"] = $"The next spawn on this level ignores the selection: {overridden}";
                    }

                    result = reply;
                }, 5000 );

                return result;
            }
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
            {
                Main.Log( $"SetBro error: {ex.Message}" );
                return new { success = false, error = ex.GetBaseException().Message, reason = BroErrorReason( ex ) };
            }
        }

        public static object ListBros( int playerNum = 0 )
        {
            try
            {
                if ( playerNum < Nums.PLAYER_MIN || playerNum >= Nums.PLAYER_MAX )
                {
                    return new { success = false, error = "Player number must be 0-3", reason = "bad-player" };
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
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
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
                int fromMap = 0;

                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    string sceneName = SceneManager.GetActiveScene().name;

                    string notPlaying = NotPlayingReason();
                    if ( notPlaying != null )
                    {
                        result = new { success = false, error = $"Cannot restart: {notPlaying}", scene = sceneName };
                        return;
                    }

                    if ( GameModeController.Instance == null )
                    {
                        result = new { success = false, error = "Cannot restart: the level has no GameModeController, so the game's restart does nothing", scene = sceneName };
                        return;
                    }

                    if ( !Connect.IsHost )
                    {
                        result = new { success = false, error = "Cannot restart: this game is an online client, and only the host restarts a level", scene = sceneName };
                        return;
                    }

                    Map current = Map.Instance;
                    fromMap = current != null ? current.GetInstanceID() : 0;

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

                    ReleasePause();
                    GameModeController.RestartLevel();

                    result = JObject.FromObject( new
                    {
                        success = true,
                        scene = sceneName
                    } );
                }, 5000 );

                JObject reply = result as JObject;
                if ( reply == null )
                {
                    return result;
                }

                string pending = AwaitLevelInPlay( () => LevelPending( fromMap, null, 0 ), reply );
                if ( pending != null )
                {
                    reply["success"] = false;
                    reply["error"] = $"The restart was requested but the level was not in play again after {LevelArrivalBudgetMs / 1000}s: {pending}";
                    reply["reason"] = "not-loaded";
                }

                return reply;
            }
            catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )
            {
                Main.Log( $"RestartLevel error: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }
        #endregion
    }
}
