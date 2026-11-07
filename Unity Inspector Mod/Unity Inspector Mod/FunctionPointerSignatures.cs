using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Unity_Inspector_Mod
{
    internal static class FunctionPointerSignatures
    {
        internal const string Hazard = "this game's Mono runtime ends the game when it reflects over a function-pointer type";

        private static readonly object Gate = new object();

        private static bool readerFailureLogged;

        internal static string Describe( MemberInfo member )
        {
            return Verdict( member, false );
        }

        internal static string DescribeLocals( MethodBase method )
        {
            return Verdict( method, true );
        }

        private static string Verdict( MemberInfo member, bool locals )
        {
            if ( !( member is MethodBase ) && !( member is PropertyInfo ) )
            {
                return null;
            }

            lock ( Gate )
            {
                try
                {
                    return Reader.Verdict( member, locals );
                }
                catch ( Exception e )
                {
                    if ( !readerFailureLogged )
                    {
                        readerFailureLogged = true;
                        Main.Log( "Could not check members for function-pointer signatures (" + e.GetType().Name + ": " + e.Message +
                                  ") — script reflection over such a member, as in 0Harmony's MonoMod internals, ends the game" );
                    }
                    return null;
                }
            }
        }

        private static class Reader
        {
            private sealed class Entry
            {
                public string Path;
                public ModuleDefMD Definition;
                public bool FailureLogged;
                public readonly Dictionary<int, string> Signatures = new Dictionary<int, string>();
                public readonly Dictionary<int, string> Locals = new Dictionary<int, string>();
            }

            private static readonly Dictionary<Module, Entry> modules = new Dictionary<Module, Entry>();

            public static string Verdict( MemberInfo member, bool locals )
            {
                // Reads only Name, ReflectedType and MetadataToken on the member: every other read on one with a function-pointer signature ends the game.
                Type reflectedType = member.ReflectedType;
                // A member with no ReflectedType, such as a DynamicMethod, has no metadata token to read.
                if ( reflectedType == null )
                {
                    return null;
                }

                string name = member.Name;
                int token = member.MetadataToken;
                for ( Type type = reflectedType; type != null; type = type.BaseType )
                {
                    Type definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                    Entry module = Open( definition.Module );
                    if ( module == null )
                    {
                        continue;
                    }

                    try
                    {
                        IMemberDef found = module.Definition.ResolveToken( (uint)token ) as IMemberDef;
                        if ( found == null || found.Name.String != name || found.DeclaringType == null ||
                             found.DeclaringType.MDToken.Raw != (uint)definition.MetadataToken )
                        {
                            continue;
                        }

                        Dictionary<int, string> verdicts = locals ? module.Locals : module.Signatures;
                        string verdict;
                        if ( !verdicts.TryGetValue( token, out verdict ) )
                        {
                            verdict = locals ? DescribeLocals( found as MethodDef ) : Describe( found );
                            verdicts[token] = verdict;
                        }
                        return verdict;
                    }
                    catch ( Exception e )
                    {
                        if ( !module.FailureLogged )
                        {
                            module.FailureLogged = true;
                            Main.Log( "dnlib failed reading a member of " + module.Path + " (" + e.GetType().Name + ": " + e.Message +
                                      ") — script reflection over its members is not checked for function-pointer signatures" );
                        }
                        return null;
                    }
                }

                return null;
            }

            private static Entry Open( Module reflected )
            {
                Entry module;
                if ( modules.TryGetValue( reflected, out module ) )
                {
                    return module;
                }

                string path = reflected is ModuleBuilder ? null : reflected.FullyQualifiedName;
                if ( path != null && File.Exists( path ) )
                {
                    try
                    {
                        module = new Entry { Path = path, Definition = ModuleDefMD.Load( File.ReadAllBytes( path ) ) };
                    }
                    catch ( Exception e )
                    {
                        Main.Log( "dnlib could not read " + path + " (" + e.GetType().Name + ": " + e.Message +
                                  ") — script reflection over its members is not checked for function-pointer signatures" );
                    }
                }

                modules[reflected] = module;
                return module;
            }

            private static string Describe( IMemberDef member )
            {
                MethodDef method = member as MethodDef;
                if ( method != null )
                {
                    return DescribeSignature( method );
                }

                PropertyDef property = member as PropertyDef;
                if ( property == null )
                {
                    return null;
                }
                if ( IsFunctionPointer( property.PropertySig?.RetType ) )
                {
                    return "the type of the property " + property.DeclaringType.ReflectionFullName + "." + property.Name + " is a function pointer";
                }

                return property.GetMethods.Concat( property.SetMethods ).Select( DescribeSignature ).FirstOrDefault( verdict => verdict != null );
            }

            private static string DescribeSignature( MethodDef method )
            {
                MethodSig signature = method.MethodSig;
                if ( signature == null )
                {
                    return null;
                }
                if ( IsFunctionPointer( signature.RetType ) )
                {
                    return "the return type of " + Name( method ) + " is a function pointer";
                }

                for ( int i = 0; i < signature.Params.Count; i++ )
                {
                    if ( IsFunctionPointer( signature.Params[i] ) )
                    {
                        return "parameter " + i + " of " + Name( method ) + " is a function pointer";
                    }
                }

                return null;
            }

            private static string DescribeLocals( MethodDef method )
            {
                if ( method == null || !method.HasBody )
                {
                    return null;
                }

                LocalList variables = method.Body.Variables;
                for ( int i = 0; i < variables.Count; i++ )
                {
                    if ( IsFunctionPointer( variables[i].Type ) )
                    {
                        return "local " + i + " of " + Name( method ) + " is a function pointer";
                    }
                }

                return null;
            }

            // Mono compares a nested function-pointer type by class pointer; only one reached through modifiers, byrefs and pointers reaches the comparator that aborts.
            private static bool IsFunctionPointer( TypeSig signature )
            {
                while ( signature is ModifierSig || signature is PinnedSig || signature is ByRefSig || signature is PtrSig )
                {
                    signature = signature.Next;
                }

                return signature is FnPtrSig;
            }

            private static string Name( MethodDef method )
            {
                return method.DeclaringType.ReflectionFullName + "." + method.Name;
            }
        }
    }
}
