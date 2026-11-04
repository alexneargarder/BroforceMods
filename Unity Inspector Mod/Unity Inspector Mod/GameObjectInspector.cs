using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public static class GameObjectInspector
    {
        public static List<GameObject> GetRootGameObjects( bool includeInactive )
        {
            var rootObjects = new List<GameObject>();

            var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach ( var transform in allTransforms )
            {
                if ( transform.parent == null && transform.gameObject != null )
                {
                    if ( includeInactive || transform.gameObject.activeInHierarchy )
                    {
                        rootObjects.Add( transform.gameObject );
                    }
                }
            }

            return rootObjects.OrderBy( go => go.name ).ToList();
        }

        public const int MaxChildrenListed = 100;
        public const int DetailedComponentsBudget = 30000;

        public static object InspectGameObject( GameObject go, bool detailed = false )
        {
            if ( go == null ) return null;

            var components = go.GetComponents<Component>();
            var componentList = new List<object>();

            if ( detailed )
            {
                int remaining = DetailedComponentsBudget;
                int left = components.Count( c => c != null );

                foreach ( var component in components )
                {
                    if ( component == null ) continue;

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
                    if ( component == null ) continue;
                    componentList.Add( new
                    {
                        type = component.GetType().Name,
                        fullType = component.GetType().FullName
                    } );
                }
            }

            var children = new List<object>();
            foreach ( Transform child in go.transform )
            {
                if ( children.Count == MaxChildrenListed ) break;

                children.Add( new
                {
                    name = child.name,
                    path = GetPath( child.gameObject ),
                    childCount = child.childCount
                } );
            }

            var result = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["path"] = GetPath( go ),
                ["isActive"] = go.activeSelf,
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
            if ( go == null ) return null;

            Component component = FindComponent( go, componentTypeName );

            if ( component == null )
            {
                throw new ArgumentException( $"Component {componentTypeName} not found on GameObject {go.name}" );
            }

            if ( string.IsNullOrEmpty( memberPath ) )
            {
                return InspectComponent( component );
            }

            return InspectMember( component, memberPath );
        }

        public static Component FindComponent( GameObject go, string componentTypeName )
        {
            foreach ( var comp in go.GetComponents<Component>() )
            {
                if ( comp == null ) continue;

                var type = comp.GetType();
                if ( type.Name == componentTypeName || type.FullName == componentTypeName )
                {
                    return comp;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads one named member, which may be a dotted path into nested members. The bulk walk's
        /// noise filters do not apply here: a member asked for by name is a member the caller wants.
        /// </summary>
        private static object InspectMember( Component component, string memberPath )
        {
            var componentType = component.GetType();
            object owner = component;
            string leaf = memberPath;

            if ( MemberPath.IsPath( memberPath ) )
            {
                var resolution = MemberPath.ResolveOwner( component, memberPath );
                if ( !resolution.Success )
                {
                    return MemberFailure( componentType, memberPath, resolution.Reason, resolution.Error, resolution.Suggestions );
                }

                owner = resolution.Owner;
                leaf = resolution.Leaf;
            }

            var ownerType = owner.GetType();

            FieldInfo field;
            PropertyInfo property;
            if ( !MemberPath.FindMember( ownerType, leaf, out field, out property ) )
            {
                return MemberFailure( componentType, memberPath, "not-found",
                    $"No field or property named {leaf} on {ownerType.Name} or any of its base types",
                    MemberPath.Suggest( ownerType, leaf, MemberPath.PathPrefix( memberPath ) ) );
            }

            object value;
            Type memberType;
            Type declaringType;
            bool isStatic;

            if ( field != null )
            {
                memberType = field.FieldType;
                declaringType = field.DeclaringType;
                isStatic = field.IsStatic;

                if ( !TryGetFieldValue( field, owner, out value ) )
                {
                    return MemberFailure( componentType, memberPath, "error",
                        $"Could not read {ownerType.Name}.{leaf}: {memberType.Name} threw on read", null );
                }
            }
            else
            {
                memberType = property.PropertyType;
                declaringType = property.DeclaringType;
                var accessor = property.GetGetMethod() ?? property.GetGetMethod( true );
                isStatic = accessor != null && accessor.IsStatic;

                if ( !TryGetPropertyValue( property, owner, out value ) )
                {
                    var reason = value as string ?? $"{memberType.Name} threw on read";
                    return MemberFailure( componentType, memberPath, "error",
                        $"Could not read {ownerType.Name}.{leaf}: {reason}", null );
                }
            }

            object serialized;
            try
            {
                serialized = SerializeValue( value );
            }
            catch ( Exception ex )
            {
                return MemberFailure( componentType, memberPath, "error",
                    $"Could not serialize {ownerType.Name}.{leaf}: {ex.Message}", null );
            }

            return new
            {
                type = componentType.Name,
                fullType = componentType.FullName,
                success = true,
                memberPath = memberPath,
                declaringType = declaringType != null ? declaringType.Name : null,
                memberType = memberType.Name,
                isStatic = isStatic,
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

        private static bool TryGetFieldValue( System.Reflection.FieldInfo field, object component, out object value )
        {
            value = null;

            try
            {
                value = MemberPath.ReadField( field, component );
                return true;
            }
            catch
            {
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
                return null;

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

        private static bool TryGetPropertyValue( System.Reflection.PropertyInfo prop, object component, out object value )
        {
            value = null;

            var twin = SharedTwin( prop );
            if ( twin != null )
            {
                value = $"may create instance of {prop.PropertyType.Name} - read {twin.Name} instead";
                return false;
            }

            var accessor = prop.GetGetMethod() ?? prop.GetGetMethod( true );
            bool isStatic = accessor != null && accessor.IsStatic;

            try
            {
                value = prop.GetValue( isStatic ? null : component, null );
                return true;
            }
            catch
            {
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
                if ( cost > Remaining ) return false;
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
                if ( c == '\n' ) lines++;
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

        private static object InspectComponent( Component component, ListingBudget budget = null )
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

            int fieldCount = 0;
            int propCount = 0;
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
                        continue;

                    try
                    {
                        if ( field.Name.Contains( "k__BackingField" ) ||
                            field.Name.Contains( "m_" ) ||
                            field.FieldType.FullName.Contains( "System.Action" ) ||
                            field.FieldType.FullName.Contains( "System.Func" ) )
                            continue;

                        object value;
                        if ( TryGetFieldValue( field, component, out value ) )
                        {
                            AddProperty( properties, field.Name, SerializeValue( value ), budget );
                        }
                        else
                        {
                            AddProperty( properties, field.Name, $"<timeout or error reading {field.FieldType.Name}>", budget );
                        }
                        fieldCount++;
                    }
                    catch { }
                }

                var props = t.GetProperties( BindingFlags.DeclaredOnly | BindingFlags.Public
                    | BindingFlags.Instance | BindingFlags.Static );
                foreach ( var prop in props )
                {
                    if ( !seen.Add( prop.Name ) )
                        continue;

                    if ( !prop.CanRead || prop.GetIndexParameters().Length != 0 )
                        continue;

                    try
                    {
                        if ( prop.PropertyType.FullName.Contains( "System.Action" ) ||
                            prop.PropertyType.FullName.Contains( "System.Func" ) ||
                            prop.Name == "gameObject" ||
                            prop.Name == "transform" )
                            continue;

                        object value;
                        if ( TryGetPropertyValue( prop, component, out value ) )
                        {
                            AddProperty( properties, prop.Name, SerializeValue( value ), budget );
                        }
                        else if ( value is string )
                        {
                            AddProperty( properties, prop.Name, $"<not read: {value}>", budget );
                        }
                        else
                        {
                            AddProperty( properties, prop.Name, $"<timeout or error reading {prop.PropertyType.Name}>", budget );
                        }
                        propCount++;
                    }
                    catch { }
                }
            }

            var methods = new List<object>();
            var methodInfos = type.GetMethods( BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly );
            foreach ( var method in methodInfos )
            {
                if ( method.IsSpecialName ) continue;

                var parameters = method.GetParameters().Select( p => new
                {
                    name = p.Name,
                    type = p.ParameterType.Name
                } ).ToArray();

                var entry = new
                {
                    name = method.Name,
                    returnType = method.ReturnType.Name,
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

        private static object SerializeValue( object value, int depth = 0 )
        {
            if ( value == null || depth > 2 ) return null;

            var type = value.GetType();
            if ( type.BaseType == typeof( System.MulticastDelegate ) )
                return "<delegate>";

            if ( type.IsPrimitive || type == typeof( string ) )
            {
                return value;
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

            if ( value is UnityEngine.Object )
            {
                var obj = value as UnityEngine.Object;
                if ( obj != null )
                {
                    return new { type = type.Name, name = obj.name, instanceId = obj.GetInstanceID() };
                }
            }

            if ( type.IsArray )
            {
                var array = value as Array;
                if ( array != null && array.Length <= 100 )
                {
                    var items = new List<object>();
                    foreach ( var item in array )
                    {
                        items.Add( SerializeValue( item, depth + 1 ) );
                    }
                    return new { arrayType = type.GetElementType().Name, length = array.Length, items = items };
                }
                return new { arrayType = type.GetElementType().Name, length = array.Length };
            }

            return value.ToString();
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

        private class TypeResolution
        {
            public Type Type;
            public int Candidates;
        }

        private static readonly Dictionary<string, TypeResolution> componentTypeCache = new Dictionary<string, TypeResolution>();
        private static readonly object componentTypeCacheLock = new object();

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

            var results = new List<object>();
            var totalFound = 0;
            var scanned = 0;
            var scanTruncated = false;

            for ( int i = 0; i < allObjects.Length; i++ )
            {
                if ( ( i & 63 ) == 0 && stopwatch.ElapsedMilliseconds > ScanBudgetMs )
                {
                    scanTruncated = true;
                    break;
                }

                var go = allObjects[i];
                if ( go == null ) continue;

                scanned++;

                if ( filterByName && ( nameGlob != null ? !nameGlob.IsMatch( go.name ) : !go.name.Contains( namePattern ) ) ) continue;

                if ( filterByComponent )
                {
                    // GetComponent( Type ) matches subclasses and interfaces; the string overload matches the exact type name only.
                    var match = resolvedType != null
                        ? go.GetComponent( resolvedType ) != null
                        : go.GetComponent( componentType ) != null;

                    if ( !match ) continue;
                }

                totalFound++;

                if ( results.Count < maxResults )
                {
                    results.Add( new
                    {
                        name = go.name,
                        path = GetPath( go ),
                        isActive = go.activeSelf,
                        hasComponent = filterByComponent
                    } );
                }
            }

            return new
            {
                count = results.Count,
                totalFound = totalFound,
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

        private static TypeResolution ResolveComponentType( string typeName )
        {
            TypeResolution resolution;
            var resolvedNow = false;

            lock ( componentTypeCacheLock )
            {
                if ( componentTypeCache.TryGetValue( typeName, out resolution ) )
                {
                    return resolution;
                }

                Type resolved = null;
                var resolvedScore = -1;
                var candidates = 0;

                foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
                {
                    foreach ( var type in GetTypesSafely( assembly ) )
                    {
                        if ( type == null ) continue;

                        var fullNameMatch = type.FullName == typeName;
                        if ( !fullNameMatch && type.Name != typeName ) continue;
                        if ( !type.IsInterface && !typeof( Component ).IsAssignableFrom( type ) ) continue;

                        candidates++;

                        var score = ( fullNameMatch ? 2 : 0 ) + ( IsGameAssembly( type ) ? 1 : 0 );
                        if ( score > resolvedScore )
                        {
                            resolved = type;
                            resolvedScore = score;
                        }
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

        private static string GetPath( GameObject go )
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