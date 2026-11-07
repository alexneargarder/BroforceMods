using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Unity_Inspector_Mod
{
    /// <summary>
    /// One step of a dotted member path: the object the segment was read from, the member that
    /// read it, and the value it produced. The member is kept so the write-back check, the
    /// write-back itself and the read all name the same one.
    /// </summary>
    internal class MemberPathStep
    {
        public string Name;
        public object Owner;
        public FieldInfo Field;
        public PropertyInfo Property;
        public int Index = -1;
        public object Value;
        public bool IsValueType;
    }

    /// <summary>
    /// The result of walking everything but the last segment of a dotted member path.
    /// </summary>
    internal class MemberPathResolution
    {
        public bool Success;
        public object Owner;
        public string Leaf;
        public string Reason;
        public string Error;
        public string[] Suggestions;
        public readonly List<MemberPathStep> Chain = new List<MemberPathStep>();
    }

    /// <summary>
    /// Dotted member paths and did-you-mean suggestions, shared by inspect_component and
    /// modify_component. Every segment resolves through FindMember, so the member a link is
    /// checked and written through is the member it was read through.
    /// </summary>
    internal static class MemberPath
    {
        public const BindingFlags MemberFlags = BindingFlags.DeclaredOnly | BindingFlags.Public
                                                                          | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private const int MaxSuggestions = 5;
        private const int MaxDistance = 3;

        private static readonly string Separator = HarmonyExtensions.PATH_SEPARATOR.ToString();

        public static bool IsPath( string name )
        {
            return !string.IsNullOrEmpty( name ) && name.IndexOf( HarmonyExtensions.PATH_SEPARATOR ) >= 0;
        }

        public static string PathPrefix( string path )
        {
            int cut = path.LastIndexOf( HarmonyExtensions.PATH_SEPARATOR );
            return cut < 0 ? null : path.Substring( 0, cut );
        }

        public static string Qualify( string prefix, string name )
        {
            return string.IsNullOrEmpty( prefix ) ? name : prefix + Separator + name;
        }

        /// <summary>
        /// Walks every segment but the last. On success Owner holds the final segment and Chain
        /// carries the links that led to it; on failure Reason, Error and, for a name that
        /// resolves nowhere, Suggestions say why.
        /// </summary>
        public static MemberPathResolution ResolveOwner( object root, string path )
        {
            var resolution = new MemberPathResolution();
            var segments = path.Split( HarmonyExtensions.PATH_SEPARATOR );

            foreach ( var segment in segments )
            {
                if ( segment.Length == 0 )
                {
                    resolution.Reason = "error";
                    resolution.Error = $"Member path '{path}' has an empty segment";
                    return resolution;
                }
            }

            object current = root;

            for ( int i = 0; i < segments.Length - 1; i++ )
            {
                string segment = segments[i];
                string walked = string.Join( Separator, segments, 0, i + 1 );

                var currentType = current.GetType();

                int index;
                string indexError;
                if ( TryIndex( current, segment, path, out index, out indexError ) )
                {
                    if ( indexError != null )
                    {
                        resolution.Reason = "not-found";
                        resolution.Error = indexError;
                        return resolution;
                    }

                    object element = ( (IList)current )[index];
                    resolution.Chain.Add( new MemberPathStep
                    {
                        Name = segment,
                        Owner = current,
                        Index = index,
                        Value = element,
                        IsValueType = element != null && element.GetType().IsValueType
                    } );

                    if ( IsNullReference( element ) )
                    {
                        resolution.Reason = "error";
                        resolution.Error = element == null
                            ? $"Cannot resolve '{path}': '{walked}' is null"
                            : $"Cannot resolve '{path}': '{walked}' is a destroyed UnityEngine.Object";
                        return resolution;
                    }

                    current = element;
                    continue;
                }

                FieldInfo field;
                PropertyInfo property;
                if ( !FindMember( currentType, segment, out field, out property ) )
                {
                    resolution.Reason = "not-found";
                    resolution.Error = $"No field or property named {segment} on {currentType.Name} or any of its base types (resolving {path})";
                    resolution.Suggestions = Suggest( currentType, segment, i == 0 ? null : string.Join( Separator, segments, 0, i ) );
                    return resolution;
                }

                // The leaf read refuses a property whose getter hands out a per-object copy; an
                // intermediate segment must refuse it too, or the write lands in the copy.
                if ( property != null )
                {
                    var shared = GameObjectInspector.SharedTwin( property );
                    if ( shared != null )
                    {
                        resolution.Reason = "error";
                        resolution.Error = $"Cannot resolve '{path}': reading '{walked}' would instantiate a copy of {property.DeclaringType.Name}.{property.Name} - walk through {shared.Name} instead";
                        return resolution;
                    }
                }

                object value;
                try
                {
                    value = ReadMember( field, property, current );
                }
                catch ( Exception ex )
                {
                    resolution.Reason = "error";
                    resolution.Error = $"Could not read '{walked}' while resolving '{path}': {Unwrap( ex ).Message}";
                    return resolution;
                }

                resolution.Chain.Add( new MemberPathStep
                {
                    Name = segment,
                    Owner = current,
                    Field = field,
                    Property = property,
                    Value = value,
                    IsValueType = value != null && value.GetType().IsValueType
                } );

                if ( IsNullReference( value ) )
                {
                    resolution.Reason = "error";
                    resolution.Error = value == null
                        ? $"Cannot resolve '{path}': '{walked}' is null"
                        : $"Cannot resolve '{path}': '{walked}' is a destroyed UnityEngine.Object";
                    return resolution;
                }

                current = value;
            }

            resolution.Success = true;
            resolution.Owner = current;
            resolution.Leaf = segments[segments.Length - 1];
            return resolution;
        }

        /// <summary>
        /// A value-type link hands out a boxed copy, so a write past one is only visible once it
        /// is stored back. Reports the link that would swallow the write, before anything is written.
        /// </summary>
        public static string CheckWriteBack( MemberPathResolution resolution, out string reason )
        {
            reason = null;

            for ( int i = resolution.Chain.Count - 1; i >= 0; i-- )
            {
                var step = resolution.Chain[i];
                if ( !step.IsValueType )
                {
                    break;
                }

                if ( step.Index >= 0 )
                {
                    if ( ( (IList)step.Owner ).IsReadOnly )
                    {
                        reason = "read-only";
                        return $"Cannot store a change back through '{step.Name}': the {step.Owner.GetType().Name} is read-only";
                    }
                }
                else if ( step.Field != null )
                {
                    if ( step.Field.IsLiteral )
                    {
                        reason = "read-only";
                        return $"Cannot store a change back through '{step.Name}': {step.Field.DeclaringType.Name}.{step.Field.Name} is a constant";
                    }
                }
                else if ( !step.Property.CanWrite )
                {
                    reason = "read-only";
                    return $"Cannot store a change back through '{step.Name}': {step.Property.DeclaringType.Name}.{step.Property.Name} is a {step.Property.PropertyType.Name} with no setter";
                }
            }

            return null;
        }

        public static void WriteBack( MemberPathResolution resolution )
        {
            for ( int i = resolution.Chain.Count - 1; i >= 0; i-- )
            {
                var step = resolution.Chain[i];
                if ( !step.IsValueType )
                {
                    break;
                }

                if ( step.Index >= 0 )
                {
                    ( (IList)step.Owner )[step.Index] = step.Value;
                }
                else if ( step.Field != null )
                {
                    step.Field.SetValue( step.Field.IsStatic ? null : step.Owner, step.Value );
                }
                else
                {
                    var setter = step.Property.GetSetMethod( true );
                    step.Property.SetValue( setter != null && setter.IsStatic ? null : step.Owner, step.Value, null );
                }
            }
        }

        private static object ReadMember( FieldInfo field, PropertyInfo property, object owner )
        {
            if ( field != null )
            {
                return ReadField( field, owner );
            }

            var accessor = property.GetGetMethod() ?? property.GetGetMethod( true );
            if ( accessor == null )
            {
                throw new InvalidOperationException( $"{property.DeclaringType.Name}.{property.Name} has no getter" );
            }

            return property.GetValue( accessor.IsStatic ? null : owner, null );
        }

        public static bool TryIndex( object owner, string segment, string path, out int index, out string error )
        {
            index = -1;
            error = null;

            var list = owner as IList;
            if ( list == null || segment.Length == 0 )
            {
                return false;
            }
            foreach ( char c in segment )
            {
                if ( c < '0' || c > '9' )
                {
                    return false;
                }
            }

            if ( !int.TryParse( segment, out index ) || index >= list.Count )
            {
                error = $"Index {segment} is out of range in '{path}': the {owner.GetType().Name} has {list.Count} items";
            }
            return true;
        }

        public static Type ElementType( Type listType )
        {
            if ( listType.IsArray )
            {
                return listType.GetElementType();
            }
            foreach ( var face in listType.GetInterfaces() )
            {
                if ( face.IsGenericType && face.GetGenericTypeDefinition() == typeof( IList<> ) )
                {
                    return face.GetGenericArguments()[0];
                }
            }
            return typeof( object );
        }

        public static object ReadField( FieldInfo field, object owner )
        {
            // A script's null const has no default recorded, and GetValue on it aborts the runtime instead of throwing.
            if ( field.IsLiteral && ( field.Attributes & FieldAttributes.HasDefault ) == 0 )
            {
                return null;
            }

            return field.GetValue( field.IsStatic ? null : owner );
        }

        // Reflection reports a member's own failure wrapped; the inner message is the useful one.
        public static Exception Unwrap( Exception ex )
        {
            var invocation = ex as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : ex;
        }

        /// <summary>
        /// The same most-derived-first walk the inspector and the modifier use: per level, fields
        /// before properties, public or private, instance or static.
        /// </summary>
        public static bool FindMember( Type type, string name, out FieldInfo field, out PropertyInfo property )
        {
            for ( Type t = type; t != null; t = t.BaseType )
            {
                try
                {
                    field = t.GetField( name, MemberFlags );
                    if ( field != null )
                    {
                        property = null;
                        return true;
                    }

                    property = t.GetProperty( name, MemberFlags );
                    if ( property != null )
                    {
                        field = null;
                        return true;
                    }
                }
                catch ( AmbiguousMatchException )
                {
                }
            }

            field = null;
            property = null;
            return false;
        }

        public static string[] Suggest( Type type, string name, string prefix )
        {
            var candidates = new List<string>();
            var seen = new HashSet<string>();

            for ( Type t = type; t != null; t = t.BaseType )
            {
                try
                {
                    foreach ( var field in t.GetFields( MemberFlags ) )
                    {
                        AddCandidate( candidates, seen, field.Name );
                    }

                    foreach ( var property in t.GetProperties( MemberFlags ) )
                    {
                        // An indexer is not reachable by name and an unreadable property is not worth walking to.
                        if ( !property.CanRead )
                        {
                            continue;
                        }

                        try
                        {
                            if ( ScriptCompiler.GuardedGetIndexParameters( property ).Length != 0 )
                            {
                                continue;
                            }
                        }
                        catch ( Exception )
                        {
                            continue;
                        }

                        AddCandidate( candidates, seen, property.Name );
                    }
                }
                catch ( Exception )
                {
                }
            }

            var suggestions = new List<string>();
            foreach ( string closest in ClosestNames( candidates, name ) )
            {
                suggestions.Add( Qualify( prefix, closest ) );
            }

            return suggestions.ToArray();
        }

        public static string[] ClosestNames( IEnumerable<string> candidates, string name )
        {
            string target = name.ToLowerInvariant();
            var ranked = new List<KeyValuePair<int, string>>();
            var seen = new HashSet<string>();

            foreach ( var candidate in candidates )
            {
                if ( string.IsNullOrEmpty( candidate ) || !seen.Add( candidate ) )
                {
                    continue;
                }

                string lowered = candidate.ToLowerInvariant();
                bool contained = lowered.Contains( target ) || target.Contains( lowered );

                if ( !contained && System.Math.Abs( lowered.Length - target.Length ) > MaxDistance )
                {
                    continue;
                }

                int distance = LevenshteinDistance( target, lowered );
                if ( !contained && distance > MaxDistance )
                {
                    continue;
                }

                ranked.Add( new KeyValuePair<int, string>( distance, candidate ) );
            }

            ranked.Sort( ( left, right ) => left.Key != right.Key
                ? left.Key.CompareTo( right.Key )
                : string.CompareOrdinal( left.Value, right.Value ) );

            var closest = new List<string>();
            foreach ( var entry in ranked )
            {
                closest.Add( entry.Value );
                if ( closest.Count == MaxSuggestions )
                {
                    break;
                }
            }

            return closest.ToArray();
        }

        public static bool IsNullReference( object value )
        {
            if ( value == null )
            {
                return true;
            }

            if ( value is UnityEngine.Object )
            {
                return (UnityEngine.Object)value == null;
            }

            return false;
        }

        private static void AddCandidate( List<string> candidates, HashSet<string> seen, string name )
        {
            // Backing fields and other compiler-generated names are never worth suggesting.
            if ( name.IndexOf( '<' ) >= 0 )
            {
                return;
            }

            if ( seen.Add( name ) )
            {
                candidates.Add( name );
            }
        }

        private static int LevenshteinDistance( string a, string b )
        {
            if ( a.Length == 0 )
            {
                return b.Length;
            }

            if ( b.Length == 0 )
            {
                return a.Length;
            }

            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];

            for ( int j = 0; j <= b.Length; j++ )
            {
                previous[j] = j;
            }

            for ( int i = 1; i <= a.Length; i++ )
            {
                current[0] = i;

                for ( int j = 1; j <= b.Length; j++ )
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int deletion = previous[j] + 1;
                    int insertion = current[j - 1] + 1;
                    int substitution = previous[j - 1] + cost;
                    current[j] = System.Math.Min( System.Math.Min( deletion, insertion ), substitution );
                }

                var swap = previous;
                previous = current;
                current = swap;
            }

            return previous[b.Length];
        }
    }
}
