using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        public static object InspectGameObject( GameObject go, bool detailed = false )
        {
            if ( go == null ) return null;

            var components = go.GetComponents<Component>();
            var componentList = new List<object>();

            if ( detailed )
            {
                foreach ( var component in components )
                {
                    if ( component == null ) continue;

                    try
                    {
                        componentList.Add( InspectComponent( component ) );
                    }
                    catch ( Exception ex )
                    {
                        componentList.Add( new
                        {
                            type = component.GetType().Name,
                            error = $"Failed to inspect: {ex.Message}"
                        } );
                    }
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

            return result;
        }

        public static object InspectSpecificComponent( GameObject go, string componentTypeName )
        {
            if ( go == null ) return null;

            Component component = null;
            foreach ( var comp in go.GetComponents<Component>() )
            {
                if ( comp != null && comp.GetType().Name == componentTypeName )
                {
                    component = comp;
                    break;
                }
            }

            if ( component == null )
            {
                throw new ArgumentException( $"Component {componentTypeName} not found on GameObject {go.name}" );
            }

            return InspectComponent( component );
        }

        private static bool TryGetFieldValue(System.Reflection.FieldInfo field, Component component, out object value)
        {
            value = null;
            
            try
            {
                value = field.GetValue(component);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private static bool TryGetPropertyValue(System.Reflection.PropertyInfo prop, Component component, out object value)
        {
            value = null;
            
            // Check if the property getter might have side effects or create new instances
            // In Unity, certain properties like MeshFilter.mesh create new instances when accessed
            var getMethod = prop.GetGetMethod();
            if (getMethod != null)
            {
                // Check if this is a property that returns the same type as a shared/prefixed version
                // (e.g., mesh vs sharedMesh, material vs sharedMaterial)
                var propName = prop.Name;
                var sharedVersion = "shared" + char.ToUpper(propName[0]) + propName.Substring(1);
                var allProps = prop.DeclaringType.GetProperties();
                
                foreach (var otherProp in allProps)
                {
                    if (otherProp.Name.Equals(sharedVersion, StringComparison.OrdinalIgnoreCase) &&
                        otherProp.PropertyType == prop.PropertyType)
                    {
                        // This property has a "shared" version, which means the non-shared version
                        // likely creates instances. Skip it.
                        value = "<skipped - may create instance>";
                        return false;
                    }
                }
            }
            
            try
            {
                value = prop.GetValue(component, null);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private static object InspectComponent( Component component )
        {
            var type = component.GetType();
            var properties = new Dictionary<string, object>();

            // Skip certain problematic types that cause crashes
            if (type.Name.Contains("TestVanDammeAnim") || 
                type.Name.Contains("NetworkedPlayer") ||
                type.FullName.Contains("Rewired"))
            {
                return new
                {
                    type = type.Name,
                    fullType = type.FullName,
                    properties = new Dictionary<string, object> { ["_skipped"] = "Complex type skipped to prevent crash" }
                };
            }

            var fields = type.GetFields( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
            int fieldCount = 0;
            foreach ( var field in fields )
            {
                try
                {
                    // Skip backing fields and complex types
                    if (field.Name.Contains("k__BackingField") || 
                        field.Name.Contains("m_") ||
                        field.FieldType.FullName.Contains("System.Action") ||
                        field.FieldType.FullName.Contains("System.Func"))
                        continue;

                    object value;
                    if (TryGetFieldValue(field, component, out value))
                    {
                        properties[field.Name] = SerializeValue(value);
                        }
                    else
                    {
                        properties[field.Name] = $"<timeout or error reading {field.FieldType.Name}>";
                        }
                    fieldCount++;
                }
                catch { }
            }

            var props = type.GetProperties( BindingFlags.Public | BindingFlags.Instance );
            int propCount = 0;
            foreach ( var prop in props )
            {
                if ( prop.CanRead && prop.GetIndexParameters().Length == 0 )
                {
                    try
                    {
                        // Skip complex properties that might cause issues
                        if (prop.PropertyType.FullName.Contains("System.Action") ||
                            prop.PropertyType.FullName.Contains("System.Func") ||
                            prop.Name == "gameObject" || 
                            prop.Name == "transform")
                            continue;

                        object value;
                        if (TryGetPropertyValue(prop, component, out value))
                        {
                            properties[prop.Name] = SerializeValue(value);
                            }
                        else
                        {
                            properties[prop.Name] = $"<timeout or error reading {prop.PropertyType.Name}>";
                            }
                        propCount++;
                    }
                    catch { }
                }
            }

            // Get methods
            var methods = new List<object>();
            var methodInfos = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methodInfos)
            {
                if (method.IsSpecialName) continue; // Skip property getters/setters
                
                var parameters = method.GetParameters().Select(p => new 
                { 
                    name = p.Name, 
                    type = p.ParameterType.Name 
                }).ToArray();
                
                methods.Add(new
                {
                    name = method.Name,
                    returnType = method.ReturnType.Name,
                    parameters = parameters
                });
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
            // Skip delegates and events
            if (type.BaseType == typeof(System.MulticastDelegate))
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

        public static object QueryGameObjects( string namePattern, string componentType, bool includeInactive, int maxResults )
        {
            IEnumerable<GameObject> allObjects;

            if ( includeInactive )
            {
                allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            }
            else
            {
                allObjects = GameObject.FindObjectsOfType<GameObject>();
            }

            if ( !string.IsNullOrEmpty( namePattern ) )
            {
                allObjects = allObjects.Where( go => go.name.Contains( namePattern ) );
            }

            if ( !string.IsNullOrEmpty( componentType ) )
            {
                allObjects = allObjects.Where( go => go.GetComponent( componentType ) != null );
            }

            var results = allObjects.Take( maxResults ).Select( go => new
            {
                name = go.name,
                path = GetPath( go ),
                isActive = go.activeSelf,
                hasComponent = !string.IsNullOrEmpty( componentType )
            } ).ToArray();

            return new
            {
                count = results.Length,
                totalFound = allObjects.Count(),
                results = results
            };
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