using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public sealed class ResolvedGameObject
    {
        public GameObject GameObject;
        public int Matches;
    }

    public static class GameObjectInspector
    {
        public const int MaxChildrenListed = 100;
        public const int DetailedComponentsBudget = 30000;
        public const int ReplyBudget = 40000;

        private const int MaxCollectionItems = 100;
        private const int MaxSerializeDepth = 2;
        private const int MaxSerializedNodes = 300;
        private const int MaxNestedFields = 50;

        // Unity numbers an object loaded from a file positive and one made at runtime negative; a runtime object flagged DontSave has no scene either.
        public static bool IsAsset( GameObject go )
        {
            return !go.scene.IsValid() && go.GetInstanceID() > 0;
        }

        public static List<GameObject> GetRootGameObjects( bool includeInactive )
        {
            var rootObjects = new List<GameObject>();

            // FindObjectsOfTypeAll also returns prefab assets, which belong to no scene.
            foreach ( var transform in Resources.FindObjectsOfTypeAll<Transform>() )
            {
                if ( transform == null || transform.parent != null )
                {
                    continue;
                }

                var go = transform.gameObject;
                if ( !go.scene.IsValid() )
                {
                    continue;
                }

                if ( includeInactive || go.activeInHierarchy )
                {
                    rootObjects.Add( go );
                }
            }

            return rootObjects.OrderBy( go => go.name, StringComparer.Ordinal ).ToList();
        }

        public static object ListGameObjects( bool includeInactive, int maxResults )
        {
            var rootObjects = GetRootGameObjects( includeInactive );
            var entries = new List<object>();
            string truncatedBy = rootObjects.Count > maxResults ? "maxResults" : null;
            int remaining = ReplyBudget;

            foreach ( var go in rootObjects )
            {
                if ( entries.Count >= maxResults )
                {
                    break;
                }

                var entry = new
                {
                    name = go.name,
                    path = GetPath( go ),
                    instanceId = go.GetInstanceID(),
                    scene = go.scene.name,
                    isActive = go.activeSelf,
                    activeInHierarchy = go.activeInHierarchy,
                    childCount = go.transform.childCount
                };

                int cost = ListingCost( "", entry );
                if ( cost > remaining )
                {
                    truncatedBy = "size";
                    break;
                }
                remaining -= cost;
                entries.Add( entry );
            }

            return new
            {
                totalCount = rootObjects.Count,
                returnedCount = entries.Count,
                truncated = truncatedBy != null,
                truncatedBy,
                gameObjects = entries.ToArray()
            };
        }

        public static ResolvedGameObject Resolve( string path, int? instanceId )
        {
            if ( instanceId.HasValue )
            {
                var byId = FindByInstanceId( instanceId.Value );
                if ( byId == null )
                {
                    throw new CallerErrorException( $"No GameObject or component has instanceId {instanceId.Value}" );
                }
                return new ResolvedGameObject { GameObject = byId, Matches = 1 };
            }

            if ( string.IsNullOrEmpty( path ) )
            {
                throw new CallerErrorException( "GameObject path is required" );
            }

            var matches = FindByPath( path );
            if ( matches.Count == 0 )
            {
                throw new CallerErrorException( $"GameObject not found: {path}" );
            }

            return new ResolvedGameObject { GameObject = matches[0], Matches = matches.Count };
        }

        public static List<GameObject> FindByPath( string path )
        {
            bool rooted = path.StartsWith( "/", StringComparison.Ordinal );
            string tail = "/" + path;
            var ranked = new List<KeyValuePair<int, GameObject>>();

            foreach ( var transform in Resources.FindObjectsOfTypeAll<Transform>() )
            {
                if ( transform == null )
                {
                    continue;
                }
                // A GameObject's name may hold '/'.
                if ( !path.EndsWith( transform.name, StringComparison.Ordinal ) )
                {
                    continue;
                }

                var go = transform.gameObject;
                string full = GetPath( go );
                bool match = rooted ? full == path : full.EndsWith( tail, StringComparison.Ordinal );
                if ( !match )
                {
                    continue;
                }

                int rank = IsAsset( go ) ? 2 : go.activeInHierarchy ? 0 : 1;
                ranked.Add( new KeyValuePair<int, GameObject>( rank, go ) );
            }

            return ranked.OrderBy( entry => entry.Key ).Select( entry => entry.Value ).ToList();
        }

        private static GameObject FindByInstanceId( int instanceId )
        {
            foreach ( var go in Resources.FindObjectsOfTypeAll<GameObject>() )
            {
                if ( go != null && go.GetInstanceID() == instanceId )
                {
                    return go;
                }
            }

            foreach ( var component in Resources.FindObjectsOfTypeAll<Component>() )
            {
                if ( component != null && component.GetInstanceID() == instanceId )
                {
                    return component.gameObject;
                }
            }

            return null;
        }

        public static Dictionary<string, object> AddLocation( ResolvedGameObject target, Dictionary<string, object> reply )
        {
            var go = target.GameObject;
            reply["instanceId"] = go.GetInstanceID();
            reply["scene"] = go.scene.IsValid() ? go.scene.name : null;
            if ( IsAsset( go ) )
            {
                reply["isAsset"] = true;
            }
            if ( target.Matches > 1 )
            {
                reply["matches"] = target.Matches;
            }
            return reply;
        }

        public static JObject WithLocation( ResolvedGameObject target, object reply )
        {
            var located = JObject.FromObject( reply );
            foreach ( var field in AddLocation( target, new Dictionary<string, object>() ) )
            {
                located[field.Key] = field.Value == null ? JValue.CreateNull() : JToken.FromObject( field.Value );
            }
            return located;
        }

        public static object InspectGameObject( GameObject go, bool detailed = false )
        {
            if ( go == null )
            {
                return null;
            }

            var components = go.GetComponents<Component>();
            var componentList = new List<object>();

            if ( detailed )
            {
                int remaining = DetailedComponentsBudget;
                int left = components.Count( c => c != null );

                foreach ( var component in components )
                {
                    if ( component == null )
                    {
                        continue;
                    }

                    int share = remaining / left;
                    var budget = new ListingBudget { Remaining = share };
                    left--;

                    try
                    {
                        componentList.Add( InspectComponent( component, budget ) );
                    }
                    catch ( Exception ex )
                    {
                        componentList.Add( new
                        {
                            type = component.GetType().Name,
                            error = $"Failed to inspect: {ex.Message}"
                        } );
                    }

                    remaining -= share - budget.Remaining;
                }
            }
            else
            {
                foreach ( var component in components )
                {
                    if ( component == null )
                    {
                        continue;
                    }
                    componentList.Add( new
                    {
                        type = component.GetType().Name,
                        fullType = component.GetType().FullName
                    } );
                }
            }

            var children = new List<object>();
            for ( int i = 0; i < go.transform.childCount && children.Count < MaxChildrenListed; i++ )
            {
                var child = go.transform.GetChild( i );
                children.Add( new
                {
                    name = child.name,
                    path = GetPath( child.gameObject ),
                    instanceId = child.gameObject.GetInstanceID(),
                    isActive = child.gameObject.activeSelf,
                    childCount = child.childCount
                } );
            }

            var result = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["path"] = GetPath( go ),
                ["isActive"] = go.activeSelf,
                ["activeInHierarchy"] = go.activeInHierarchy,
                ["layer"] = LayerMask.LayerToName( go.layer ),
                ["tag"] = go.tag,
                ["componentCount"] = components.Length,
                ["childCount"] = go.transform.childCount
            };

            if ( detailed )
            {
                result["transform"] = SerializeTransform( go.transform );
                result["components"] = componentList;
            }
            else
            {
                result["position"] = SerializeValue( go.transform.position );
                result["componentTypes"] = componentList;
            }

            if ( children.Count > 0 )
            {
                result["children"] = children;
            }

            if ( go.transform.childCount > children.Count )
            {
                result["childrenTruncated"] = true;
            }

            return result;
        }

        public static object InspectSpecificComponent( GameObject go, string componentTypeName, string memberPath = null )
        {
            if ( go == null )
            {
                return null;
            }

            Component component = FindComponent( go, componentTypeName );

            if ( component == null )
            {
                throw new CallerErrorException( ComponentNotFound( go, componentTypeName ) );
            }

            if ( string.IsNullOrEmpty( memberPath ) )
            {
                return InspectComponent( component, new ListingBudget { Remaining = ReplyBudget } );
            }

            return InspectMember( component, memberPath );
        }

        public static Component FindComponent( GameObject go, string componentTypeName )
        {
            var components = go.GetComponents<Component>();
            foreach ( var comp in components )
            {
                if ( comp == null )
                {
                    continue;
                }

                var type = comp.GetType();
                if ( type.Name == componentTypeName || type.FullName == componentTypeName )
                {
                    return comp;
                }
            }

            var resolved = ResolveComponentType( componentTypeName ).Type;
            if ( resolved == null )
            {
                return null;
            }

            foreach ( var comp in components )
            {
                if ( comp != null && resolved.IsInstanceOfType( comp ) )
                {
                    return comp;
                }
            }

            return null;
        }

        public static string ComponentNotFound( GameObject go, string componentTypeName )
        {
            var present = go.GetComponents<Component>().Where( c => c != null ).Select( c => c.GetType().Name ).ToArray();
            return $"Component {componentTypeName} not found on GameObject {go.name}; it has {string.Join( ", ", present )}";
        }

        public sealed class MemberRead
        {
            public bool Success;
            public object Value;
            public Type MemberType;
            public Type DeclaringType;
            public bool IsStatic;
            public string Reason;
            public string Error;
            public string[] Suggestions;
        }

        public static MemberRead ReadMember( object root, string memberPath )
        {
            object owner = root;
            string leaf = memberPath;

            if ( MemberPath.IsPath( memberPath ) )
            {
                var resolution = MemberPath.ResolveOwner( root, memberPath );
                if ( !resolution.Success )
                {
                    return new MemberRead { Reason = resolution.Reason, Error = resolution.Error, Suggestions = resolution.Suggestions };
                }

                owner = resolution.Owner;
                leaf = resolution.Leaf;
            }

            var ownerType = owner.GetType();

            int index;
            string indexError;
            if ( MemberPath.TryIndex( owner, leaf, memberPath, out index, out indexError ) )
            {
                if ( indexError != null )
                {
                    return new MemberRead { Reason = "not-found", Error = indexError };
                }

                return new MemberRead
                {
                    Success = true,
                    Value = ( (IList)owner )[index],
                    MemberType = MemberPath.ElementType( ownerType ),
                    DeclaringType = ownerType
                };
            }

            FieldInfo field;
            PropertyInfo property;
            if ( !MemberPath.FindMember( ownerType, leaf, out field, out property ) )
            {
                return new MemberRead
                {
                    Reason = "not-found",
                    Error = $"No field or property named {leaf} on {ownerType.Name} or any of its base types",
                    Suggestions = MemberPath.Suggest( ownerType, leaf, MemberPath.PathPrefix( memberPath ) )
                };
            }

            var read = new MemberRead();
            object value;
            Exception failure;

            if ( field != null )
            {
                read.MemberType = field.FieldType;
                read.DeclaringType = field.DeclaringType;
                read.IsStatic = field.IsStatic;

                if ( !TryGetFieldValue( field, owner, out value, out failure ) )
                {
                    read.Reason = "error";
                    read.Error = $"Could not read {ownerType.Name}.{leaf}: {Describe( failure )}";
                    return read;
                }
            }
            else
            {
                read.MemberType = property.PropertyType;
                read.DeclaringType = property.DeclaringType;
                var accessor = property.GetGetMethod() ?? property.GetGetMethod( true );
                read.IsStatic = accessor != null && accessor.IsStatic;

                string refusal;
                if ( !TryGetPropertyValue( property, owner, out value, out refusal, out failure ) )
                {
                    read.Reason = "error";
                    read.Error = $"Could not read {ownerType.Name}.{leaf}: {refusal ?? Describe( failure )}";
                    return read;
                }
            }

            read.Success = true;
            read.Value = value;
            return read;
        }

        private static object InspectMember( Component component, string memberPath )
        {
            var componentType = component.GetType();
            var read = ReadMember( component, memberPath );
            if ( !read.Success )
            {
                return MemberFailure( componentType, memberPath, read.Reason, read.Error, read.Suggestions );
            }

            object serialized;
            try
            {
                serialized = SerializeValue( read.Value );
            }
            catch ( Exception ex )
            {
                return MemberFailure( componentType, memberPath, "error",
                    $"Could not serialize {memberPath}: {ex.Message}", null );
            }

            return new
            {
                type = componentType.Name,
                fullType = componentType.FullName,
                success = true,
                memberPath = memberPath,
                declaringType = read.DeclaringType != null ? TypeName( read.DeclaringType ) : null,
                memberType = TypeName( read.MemberType ),
                isStatic = read.IsStatic,
                value = serialized
            };
        }

        private static object MemberFailure( Type componentType, string memberPath, string reason, string error, string[] suggestions )
        {
            if ( suggestions == null )
            {
                return new
                {
                    type = componentType.Name,
                    fullType = componentType.FullName,
                    success = false,
                    memberPath = memberPath,
                    reason = reason,
                    error = error
                };
            }

            return new
            {
                type = componentType.Name,
                fullType = componentType.FullName,
                success = false,
                memberPath = memberPath,
                reason = reason,
                error = error,
                suggestions = suggestions
            };
        }

        private static string Describe( Exception failure )
        {
            if ( failure == null )
            {
                return "the read failed";
            }
            var inner = MemberPath.Unwrap( failure );
            return $"{inner.GetType().Name}: {inner.Message}";
        }

        private static bool TryGetFieldValue( FieldInfo field, object component, out object value, out Exception failure )
        {
            value = null;
            failure = null;

            try
            {
                value = MemberPath.ReadField( field, component );
                return true;
            }
            catch ( Exception ex )
            {
                failure = ex;
                return false;
            }
        }

        /// <summary>
        /// Unity's non-shared accessors (mesh, material) instantiate a per-object copy on read.
        /// Returns the shared* twin that names the same value without copying, or null.
        /// </summary>
        internal static PropertyInfo SharedTwin( PropertyInfo prop )
        {
            if ( prop.GetGetMethod() == null )
            {
                return null;
            }

            var propName = prop.Name;
            var sharedVersion = "shared" + char.ToUpper( propName[0] ) + propName.Substring( 1 );

            foreach ( var otherProp in prop.DeclaringType.GetProperties() )
            {
                if ( otherProp.Name.Equals( sharedVersion, StringComparison.OrdinalIgnoreCase ) &&
                     otherProp.PropertyType == prop.PropertyType )
                {
                    return otherProp;
                }
            }

            return null;
        }

        private static bool TryGetPropertyValue( PropertyInfo prop, object component, out object value, out string refusal, out Exception failure )
        {
            value = null;
            refusal = null;
            failure = null;

            var twin = SharedTwin( prop );
            if ( twin != null )
            {
                refusal = $"may create instance of {prop.PropertyType.Name} - read {twin.Name} instead";
                return false;
            }

            var accessor = prop.GetGetMethod() ?? prop.GetGetMethod( true );
            if ( accessor == null )
            {
                refusal = $"{prop.DeclaringType.Name}.{prop.Name} is write-only";
                return false;
            }

            try
            {
                value = prop.GetValue( accessor.IsStatic ? null : component, null );
                return true;
            }
            catch ( Exception ex )
            {
                failure = ex;
                return false;
            }
        }

        private sealed class ListingBudget
        {
            public int Remaining;
            public int OmittedProperties;
            public int OmittedMethods;

            public bool Take( string name, object serialized )
            {
                int cost = ListingCost( name, serialized );
                if ( cost > Remaining )
                {
                    return false;
                }
                Remaining -= cost;
                return true;
            }
        }

        private static int ListingCost( string name, object serialized )
        {
            string text;
            try
            {
                text = serialized == null ? "null" : JToken.FromObject( serialized ).ToString( Formatting.Indented );
            }
            catch
            {
                text = string.Empty;
            }

            int lines = 1;
            foreach ( char c in text )
            {
                if ( c == '\n' )
                {
                    lines++;
                }
            }

            return name.Length + 6 + text.Length + lines * 10;
        }

        private static void AddProperty( Dictionary<string, object> properties, string name, object serialized, ListingBudget budget )
        {
            if ( budget != null && !budget.Take( name, serialized ) )
            {
                budget.OmittedProperties++;
                return;
            }

            properties[name] = serialized;
        }

        private static object InspectComponent( Component component, ListingBudget budget )
        {
            var type = component.GetType();
            var properties = new Dictionary<string, object>();

            // Bro subclasses never match — this fires only on names containing it, in this build just the base type.
            if ( type.Name.Contains( "TestVanDammeAnim" ) )
            {
                return new
                {
                    type = type.Name,
                    fullType = type.FullName,
                    properties = new Dictionary<string, object> { ["_skipped"] = "Complex type skipped" }
                };
            }

            var seen = new HashSet<string>();

            // Most-derived first, so a shadowed member reads the way modify_component writes it.
            for ( Type t = type; t != null; t = t.BaseType )
            {
                var fields = t.GetFields( BindingFlags.DeclaredOnly | BindingFlags.Public
                                                                    | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static );
                foreach ( var field in fields )
                {
                    // Reserved before the filters, so a dropped derived member still hides its base shadow.
                    if ( !seen.Add( field.Name ) )
                    {
                        continue;
                    }

                    try
                    {
                        if ( field.Name.Contains( "k__BackingField" ) ||
                             field.Name.StartsWith( "m_", StringComparison.Ordinal ) ||
                             field.FieldType.FullName.Contains( "System.Action" ) ||
                             field.FieldType.FullName.Contains( "System.Func" ) )
                        {
                            continue;
                        }

                        object value;
                        Exception failure;
                        if ( TryGetFieldValue( field, component, out value, out failure ) )
                        {
                            AddProperty( properties, field.Name, SerializeValue( value ), budget );
                        }
                        else
                        {
                            AddProperty( properties, field.Name, $"<error reading {TypeName( field.FieldType )}: {Describe( failure )}>", budget );
                        }
                    }
                    catch { }
                }

                var props = t.GetProperties( BindingFlags.DeclaredOnly | BindingFlags.Public
                                                                       | BindingFlags.Instance | BindingFlags.Static );
                foreach ( var prop in props )
                {
                    if ( prop.GetIndexParameters().Length != 0 )
                    {
                        continue;
                    }

                    if ( !seen.Add( prop.Name ) )
                    {
                        continue;
                    }

                    if ( !prop.CanRead )
                    {
                        continue;
                    }

                    try
                    {
                        if ( prop.PropertyType.FullName.Contains( "System.Action" ) ||
                             prop.PropertyType.FullName.Contains( "System.Func" ) ||
                             prop.Name == "gameObject" ||
                             prop.Name == "transform" )
                        {
                            continue;
                        }

                        // A static getter is game code that can create objects or run a static constructor.
                        var accessor = prop.GetGetMethod() ?? prop.GetGetMethod( true );
                        if ( accessor != null && accessor.IsStatic )
                        {
                            AddProperty( properties, prop.Name, "<static property: read it with memberPath>", budget );
                            continue;
                        }

                        object value;
                        string refusal;
                        Exception failure;
                        if ( TryGetPropertyValue( prop, component, out value, out refusal, out failure ) )
                        {
                            AddProperty( properties, prop.Name, SerializeValue( value ), budget );
                        }
                        else if ( refusal != null )
                        {
                            AddProperty( properties, prop.Name, $"<not read: {refusal}>", budget );
                        }
                        else
                        {
                            AddProperty( properties, prop.Name, $"<error reading {TypeName( prop.PropertyType )}: {Describe( failure )}>", budget );
                        }
                    }
                    catch { }
                }
            }

            var methods = new List<object>();
            var methodInfos = type.GetMethods( BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly );
            foreach ( var method in methodInfos )
            {
                if ( method.IsSpecialName )
                {
                    continue;
                }

                var parameters = method.GetParameters().Select( p => new
                {
                    name = p.Name,
                    type = TypeName( p.ParameterType )
                } ).ToArray();

                var entry = new
                {
                    name = method.Name,
                    returnType = TypeName( method.ReturnType ),
                    parameters = parameters
                };

                if ( budget != null && !budget.Take( method.Name, entry ) )
                {
                    budget.OmittedMethods++;
                    continue;
                }

                methods.Add( entry );
            }

            if ( budget != null && budget.OmittedProperties + budget.OmittedMethods > 0 )
            {
                return new
                {
                    type = type.Name,
                    fullType = type.FullName,
                    properties = properties,
                    methods = methods,
                    truncated = true,
                    omittedProperties = budget.OmittedProperties,
                    omittedMethods = budget.OmittedMethods
                };
            }

            return new
            {
                type = type.Name,
                fullType = type.FullName,
                properties = properties,
                methods = methods
            };
        }

        public static string TypeName( Type type )
        {
            if ( type == null )
            {
                return null;
            }
            if ( type.IsArray )
            {
                return TypeName( type.GetElementType() ) + "[" + new string( ',', type.GetArrayRank() - 1 ) + "]";
            }
            if ( !type.IsGenericType )
            {
                return type.Name;
            }

            string name = type.Name;
            int tick = name.IndexOf( '`' );
            if ( tick >= 0 )
            {
                name = name.Substring( 0, tick );
            }
            return name + "<" + string.Join( ", ", type.GetGenericArguments().Select( a => TypeName( a ) ).ToArray() ) + ">";
        }

        private sealed class SerializeState
        {
            public int Nodes = MaxSerializedNodes;
        }

        public static object SerializeValue( object value )
        {
            return SerializeValue( value, 0, new SerializeState() );
        }

        private static object SerializeValue( object value, int depth, SerializeState state )
        {
            if ( value == null )
            {
                return null;
            }
            state.Nodes--;

            var type = value.GetType();
            if ( typeof( Delegate ).IsAssignableFrom( type ) )
            {
                return "<delegate>";
            }

            if ( type.IsPrimitive || type == typeof( string ) || type == typeof( decimal ) )
            {
                return value;
            }

            if ( type.IsEnum )
            {
                return value.ToString();
            }

            if ( type == typeof( Vector2 ) )
            {
                var v = (Vector2)value;
                return new { x = v.x, y = v.y };
            }

            if ( type == typeof( Vector3 ) )
            {
                var v = (Vector3)value;
                return new { x = v.x, y = v.y, z = v.z };
            }

            if ( type == typeof( Vector4 ) )
            {
                var v = (Vector4)value;
                return new { x = v.x, y = v.y, z = v.z, w = v.w };
            }

            if ( type == typeof( Quaternion ) )
            {
                var q = (Quaternion)value;
                var euler = q.eulerAngles;
                return new { euler = new { x = euler.x, y = euler.y, z = euler.z }, x = q.x, y = q.y, z = q.z, w = q.w };
            }

            if ( type == typeof( Color ) )
            {
                var c = (Color)value;
                return new { r = c.r, g = c.g, b = c.b, a = c.a };
            }

            if ( type == typeof( Rect ) )
            {
                var r = (Rect)value;
                return new { x = r.x, y = r.y, width = r.width, height = r.height };
            }

            if ( type == typeof( Bounds ) )
            {
                var b = (Bounds)value;
                return new { center = SerializeValue( b.center, depth + 1, state ), size = SerializeValue( b.size, depth + 1, state ) };
            }

            var unityObject = value as UnityEngine.Object;
            if ( unityObject != null )
            {
                return new { type = type.Name, name = unityObject.name, instanceId = unityObject.GetInstanceID() };
            }

            if ( value is UnityEngine.Object )
            {
                // Unity's null check fails for a destroyed object, whose managed id is still readable.
                return new { type = type.Name, destroyed = true, instanceId = ( (UnityEngine.Object)value ).GetInstanceID() };
            }

            if ( depth >= MaxSerializeDepth || state.Nodes <= 0 )
            {
                return $"<{TypeName( type )}>";
            }

            var dictionary = value as IDictionary;
            if ( dictionary != null )
            {
                var entries = new List<object>();
                bool cut = false;
                foreach ( DictionaryEntry entry in dictionary )
                {
                    if ( entries.Count == MaxCollectionItems || state.Nodes <= 0 )
                    {
                        cut = true;
                        break;
                    }
                    entries.Add( new { key = SerializeValue( entry.Key, depth + 1, state ), value = SerializeValue( entry.Value, depth + 1, state ) } );
                }
                return Collection( "dictionaryType", type, dictionary.Count, "entries", entries, cut );
            }

            var list = value as IList;
            if ( list != null )
            {
                var items = new List<object>();
                bool cut = false;
                foreach ( var item in list )
                {
                    if ( items.Count == MaxCollectionItems || state.Nodes <= 0 )
                    {
                        cut = true;
                        break;
                    }
                    items.Add( SerializeValue( item, depth + 1, state ) );
                }

                if ( type.IsArray )
                {
                    var array = (Array)value;
                    if ( cut )
                    {
                        return new { arrayType = TypeName( type.GetElementType() ), length = array.Length, items, truncated = true };
                    }
                    return new { arrayType = TypeName( type.GetElementType() ), length = array.Length, items };
                }

                return Collection( "listType", type, list.Count, "items", items, cut );
            }

            var collection = value as ICollection;
            if ( collection != null )
            {
                var items = new List<object>();
                bool cut = false;
                foreach ( var item in collection )
                {
                    if ( items.Count == MaxCollectionItems || state.Nodes <= 0 )
                    {
                        cut = true;
                        break;
                    }
                    items.Add( SerializeValue( item, depth + 1, state ) );
                }
                return Collection( "collectionType", type, collection.Count, "items", items, cut );
            }

            if ( value is IEnumerable || IsSystemType( type ) )
            {
                return value is IEnumerable ? $"<{TypeName( type )}>" : value.ToString();
            }

            return SerializeFields( value, type, depth, state );
        }

        private static bool IsSystemType( Type type )
        {
            string ns = type.Namespace;
            return ns != null && ( ns == "System" || ns.StartsWith( "System.", StringComparison.Ordinal ) );
        }

        private static object Collection( string typeKey, Type type, int count, string itemsKey, List<object> items, bool cut )
        {
            var reply = new Dictionary<string, object>
            {
                [typeKey] = TypeName( type ),
                ["count"] = count,
                [itemsKey] = items
            };
            if ( cut )
            {
                reply["truncated"] = true;
            }
            return reply;
        }

        private static object SerializeFields( object value, Type type, int depth, SerializeState state )
        {
            var fields = new Dictionary<string, object>();
            bool cut = false;

            for ( Type t = type; t != null && t != typeof( object ) && t != typeof( ValueType ); t = t.BaseType )
            {
                foreach ( var field in t.GetFields( BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance ) )
                {
                    string name = BackingFieldName( field.Name );
                    if ( fields.ContainsKey( name ) )
                    {
                        continue;
                    }

                    if ( fields.Count == MaxNestedFields || state.Nodes <= 0 )
                    {
                        cut = true;
                        break;
                    }

                    try
                    {
                        fields[name] = SerializeValue( MemberPath.ReadField( field, value ), depth + 1, state );
                    }
                    catch ( Exception ex )
                    {
                        fields[name] = $"<error reading {TypeName( field.FieldType )}: {Describe( ex )}>";
                    }
                }
            }

            if ( cut )
            {
                fields["<truncated>"] = true;
            }
            return fields;
        }

        private static string BackingFieldName( string name )
        {
            const string suffix = ">k__BackingField";
            if ( name.StartsWith( "<", StringComparison.Ordinal ) && name.EndsWith( suffix, StringComparison.Ordinal ) )
            {
                return name.Substring( 1, name.Length - 1 - suffix.Length );
            }
            return name;
        }

        private static object SerializeTransform( Transform transform )
        {
            return new
            {
                position = SerializeValue( transform.position ),
                localPosition = SerializeValue( transform.localPosition ),
                rotation = SerializeValue( transform.rotation ),
                localRotation = SerializeValue( transform.localRotation ),
                localScale = SerializeValue( transform.localScale ),
                parent = transform.parent != null ? transform.parent.name : null
            };
        }

        private const int ScanBudgetMs = 2000;
        private const string GameAssemblyName = "Assembly-CSharp";

        public sealed class TypeResolution
        {
            public Type Type;
            public int Candidates;
        }

        private static readonly Dictionary<string, TypeResolution> componentTypeCache = new Dictionary<string, TypeResolution>();
        private static readonly object componentTypeCacheLock = new object();
        private static int cachedAssemblyCount = -1;

        public static void InvalidateTypeCache()
        {
            lock ( componentTypeCacheLock )
            {
                componentTypeCache.Clear();
            }
        }

        public static object QueryGameObjects( string namePattern, string componentType, bool includeInactive, int maxResults )
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            GameObject[] allObjects;

            if ( includeInactive )
            {
                allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            }
            else
            {
                allObjects = GameObject.FindObjectsOfType<GameObject>();
            }

            var filterByName = !string.IsNullOrEmpty( namePattern );
            var nameGlob = filterByName ? NameGlob( namePattern ) : null;
            var filterByComponent = !string.IsNullOrEmpty( componentType );
            var resolution = filterByComponent ? ResolveComponentType( componentType ) : null;
            var resolvedType = resolution != null ? resolution.Type : null;

            if ( filterByComponent && resolvedType == null )
            {
                throw new CallerErrorException( $"No component type named {componentType} is loaded - componentType takes a Component class or interface's short or full name" );
            }

            var results = new List<object>();
            var totalFound = 0;
            var scanned = 0;
            var scanTruncated = false;
            string truncatedBy = null;
            int remaining = ReplyBudget;

            for ( int i = 0; i < allObjects.Length; i++ )
            {
                if ( ( i & 63 ) == 0 && stopwatch.ElapsedMilliseconds > ScanBudgetMs )
                {
                    scanTruncated = true;
                    break;
                }

                var go = allObjects[i];
                if ( go == null || !go.scene.IsValid() )
                {
                    continue;
                }

                scanned++;

                if ( filterByName && ( nameGlob != null ? !nameGlob.IsMatch( go.name ) : !go.name.Contains( namePattern ) ) )
                {
                    continue;
                }

                // GetComponent( Type ) matches subclasses and interfaces.
                if ( filterByComponent && go.GetComponent( resolvedType ) == null )
                {
                    continue;
                }

                totalFound++;

                if ( truncatedBy != null )
                {
                    continue;
                }
                if ( results.Count >= maxResults )
                {
                    truncatedBy = "maxResults";
                    continue;
                }

                var entry = new
                {
                    name = go.name,
                    path = GetPath( go ),
                    instanceId = go.GetInstanceID(),
                    scene = go.scene.name,
                    isActive = go.activeSelf,
                    activeInHierarchy = go.activeInHierarchy,
                    hasComponent = filterByComponent
                };

                int cost = ListingCost( "", entry );
                if ( cost > remaining )
                {
                    truncatedBy = "size";
                    continue;
                }
                remaining -= cost;
                results.Add( entry );
            }

            return new
            {
                count = results.Count,
                totalFound = totalFound,
                truncated = truncatedBy != null,
                truncatedBy,
                results = results.ToArray(),
                resolvedType = resolvedType != null ? resolvedType.FullName : null,
                candidates = resolution != null ? resolution.Candidates : 0,
                scanned = scanned,
                scanTruncated = scanTruncated
            };
        }

        private static Regex NameGlob( string pattern )
        {
            if ( pattern.IndexOfAny( new[] { '*', '?' } ) < 0 )
            {
                return null;
            }

            var regex = Regex.Escape( pattern ).Replace( "\\*", ".*" ).Replace( "\\?", "." );
            return new Regex( "^" + regex + "$", RegexOptions.Singleline );
        }

        // Every compile leaves a dynamic assembly behind for good; the compiler imports only the loaded scripts' and the latest declaration of each execute_code type.
        private static IEnumerable<Type> LiveTypes()
        {
            var scripts = new HashSet<Assembly>( ScriptManager.LoadedScriptAssemblies( null ) );

            foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
            {
                if ( assembly is AssemblyBuilder && !scripts.Contains( assembly ) )
                {
                    continue;
                }
                foreach ( var type in GetTypesSafely( assembly ) )
                {
                    yield return type;
                }
            }

            foreach ( var type in ScriptCompiler.LatestEvalTypes() )
            {
                yield return type;
            }
        }

        public static TypeResolution ResolveComponentType( string typeName )
        {
            TypeResolution resolution;
            var resolvedNow = false;

            lock ( componentTypeCacheLock )
            {
                int assemblyCount = AppDomain.CurrentDomain.GetAssemblies().Length;
                if ( assemblyCount != cachedAssemblyCount )
                {
                    componentTypeCache.Clear();
                    cachedAssemblyCount = assemblyCount;
                }

                if ( componentTypeCache.TryGetValue( typeName, out resolution ) )
                {
                    return resolution;
                }

                Type resolved = null;
                var resolvedScore = -1;
                var candidates = 0;

                foreach ( var type in LiveTypes() )
                {
                    if ( type == null )
                    {
                        continue;
                    }

                    var fullNameMatch = type.FullName == typeName;
                    if ( !fullNameMatch && type.Name != typeName )
                    {
                        continue;
                    }
                    if ( !type.IsInterface && !typeof( Component ).IsAssignableFrom( type ) )
                    {
                        continue;
                    }

                    candidates++;

                    var score = ( fullNameMatch ? 2 : 0 ) + ( IsGameAssembly( type ) ? 1 : 0 );
                    if ( score > resolvedScore )
                    {
                        resolved = type;
                        resolvedScore = score;
                    }
                }

                resolution = new TypeResolution { Type = resolved, Candidates = candidates };
                componentTypeCache[typeName] = resolution;
                resolvedNow = true;
            }

            if ( resolvedNow && resolution.Candidates > 1 && resolution.Type != null )
            {
                Main.Log( $"query_gameobjects: '{typeName}' matched {resolution.Candidates} types; using {resolution.Type.FullName} from {SafeAssemblyName( resolution.Type )}" );
            }

            return resolution;
        }

        private static Type[] GetTypesSafely( Assembly assembly )
        {
            try
            {
                return assembly.GetTypes();
            }
            catch ( ReflectionTypeLoadException ex )
            {
                // Partially populated: unloadable entries come back as null.
                return ex.Types ?? new Type[0];
            }
            catch ( Exception )
            {
                return new Type[0];
            }
        }

        private static bool IsGameAssembly( Type type )
        {
            return SafeAssemblyName( type ) == GameAssemblyName;
        }

        private static string SafeAssemblyName( Type type )
        {
            try
            {
                return type.Assembly.GetName().Name;
            }
            catch ( Exception )
            {
                return null;
            }
        }

        public static string GetPath( GameObject go )
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
    }
}
