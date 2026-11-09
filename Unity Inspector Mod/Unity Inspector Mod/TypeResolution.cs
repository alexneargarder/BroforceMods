using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mono.CSharp;

namespace Unity_Inspector_Mod
{
    internal static class TypeResolution
    {
        private static bool installed;

        private static readonly FieldInfo NamespaceTypes =
            typeof( Namespace ).GetField( "types", BindingFlags.Instance | BindingFlags.NonPublic );

        private static bool hideInternal;
        private static bool retryPending;
        private static bool retrying;

        private struct LookupState
        {
            public bool Hidden;
            public bool Retry;
            public bool Retrying;
        }

        internal static void Install()
        {
            if ( installed )
            {
                return;
            }
            installed = true;

            Harmony harmony = new Harmony( "scriptcompiler.internal" );
            InstallInternalTypeLookup( harmony );

            try
            {
                MethodInfo accessibleAs = AccessTools.Method( typeof( MemberCore ), "IsAccessibleAs", new[] { typeof( TypeSpec ) } );
                Type importedDefinition = typeof( Evaluator ).Assembly.GetType( "Mono.CSharp.ImportedTypeDefinition" );
                MethodInfo internalAsPublic = importedDefinition == null
                    ? null
                    : AccessTools.Method( importedDefinition, "Mono.CSharp.ITypeDefinition.IsInternalAsPublic" );
                if ( accessibleAs == null || internalAsPublic == null )
                {
                    Main.Log( "Mono.CSharp.MemberCore.IsAccessibleAs or ImportedTypeDefinition.IsInternalAsPublic not found — " +
                              InternalSignatureWarning );
                }
                else
                {
                    harmony.Patch( internalAsPublic, prefix: new HarmonyMethod( typeof( TypeResolution ), "InternalAsOwn" ) );
                    harmony.Patch( accessibleAs,
                        prefix: new HarmonyMethod( typeof( TypeResolution ), "BeginAccessCheck" ),
                        finalizer: new HarmonyMethod( typeof( TypeResolution ), "EndAccessCheck" ) );
                }
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch Mono.CSharp's accessibility check (" + e.GetType().Name + ": " + e.Message + ") — " +
                          InternalSignatureWarning );
            }

            try
            {
                MethodInfo resolveProbeType = AccessTools.DeclaredMethod( typeof( Is ), "ResolveProbeType" );
                MethodInfo resolveMatching = AccessTools.DeclaredMethod( typeof( Is ), "ResolveMatchingExpression" );
                if ( resolveProbeType == null || resolveMatching == null )
                {
                    Main.Log( "Mono.CSharp.Is.ResolveProbeType or ResolveMatchingExpression not found — " + UnresolvedIsWarning );
                }
                else
                {
                    harmony.Patch( resolveProbeType,
                        prefix: new HarmonyMethod( typeof( TypeResolution ), "KeepProbeType" ),
                        postfix: new HarmonyMethod( typeof( TypeResolution ), "ReportUnresolvedProbeType" ) );
                    harmony.Patch( resolveMatching, prefix: new HarmonyMethod( typeof( TypeResolution ), "SkipFailedProbe" ) );
                }
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch Mono.CSharp.Is (" + e.GetType().Name + ": " + e.Message + ") — " + UnresolvedIsWarning );
            }

            try
            {
                MethodInfo prepareEmit = AccessTools.DeclaredMethod( typeof( ClassOrStruct ), "PrepareEmit" );
                MethodInfo compile = AccessTools.Method( typeof( Evaluator ), "Compile", new[] { typeof( string ), typeof( CompiledMethod ).MakeByRefType() } );
                if ( prepareEmit == null || compile == null || PatternHelperField == null || ContainersField == null )
                {
                    Main.Log( "Mono.CSharp pattern helper members not found — " + NumberPatternWarning );
                }
                else
                {
                    harmony.Patch( compile, finalizer: new HarmonyMethod( typeof( TypeResolution ), "DropPatternHelper" ) );
                    harmony.Patch( prepareEmit, prefix: new HarmonyMethod( typeof( TypeResolution ), "SkipPreparedMatcher" ) );
                }
            }
            catch ( Exception e )
            {
                Main.Log( "Failed to patch Mono.CSharp's pattern helper (" + e.GetType().Name + ": " + e.Message + ") — " +
                          NumberPatternWarning );
            }
        }

        // The lookup postfix goes in last and every patch comes out on a failure: without the others it would let an internal type shadow a public one.
        private static void InstallInternalTypeLookup( Harmony harmony )
        {
            List<MethodBase> installedMethods = new List<MethodBase>();
            try
            {
                MethodInfo lookupSimple = AccessTools.Method( typeof( NamespaceContainer ), "LookupNamespaceOrType",
                    new[] { typeof( string ), typeof( int ), typeof( LookupMode ), typeof( Location ) } );
                MethodInfo importedOverride = AccessTools.Method( typeof( Namespace ), "IsImportedTypeOverride" );
                MethodInfo aliasDefine = AccessTools.DeclaredMethod( typeof( UsingAliasNamespace ), "Define" );
                MethodInfo usingTypeDefine = AccessTools.DeclaredMethod( typeof( UsingType ), "Define" );
                MethodInfo lookupType = AccessTools.Method( typeof( Namespace ), "LookupType",
                    new[] { typeof( IMemberContext ), typeof( string ), typeof( int ), typeof( LookupMode ), typeof( Location ) } );
                if ( lookupSimple == null || importedOverride == null || aliasDefine == null || usingTypeDefine == null ||
                     lookupType == null || NamespaceTypes == null )
                {
                    Main.Log( "Mono.CSharp type lookup members not found — " + InternalTypesWarning );
                    return;
                }

                installedMethods.Add( lookupSimple );
                harmony.Patch( lookupSimple,
                    prefix: new HarmonyMethod( typeof( TypeResolution ), "BeginSimpleLookup" ),
                    postfix: new HarmonyMethod( typeof( TypeResolution ), "RetrySimpleLookup" ),
                    finalizer: new HarmonyMethod( typeof( TypeResolution ), "EndLookupScope" ) );
                installedMethods.Add( importedOverride );
                harmony.Patch( importedOverride, prefix: new HarmonyMethod( typeof( TypeResolution ), "AmbiguousInternalTypes" ) );
                foreach ( MethodInfo define in new[] { aliasDefine, usingTypeDefine } )
                {
                    installedMethods.Add( define );
                    harmony.Patch( define,
                        prefix: new HarmonyMethod( typeof( TypeResolution ), "BeginUsingDefine" ),
                        finalizer: new HarmonyMethod( typeof( TypeResolution ), "EndLookupScope" ) );
                }
                installedMethods.Add( lookupType );
                harmony.Patch( lookupType, postfix: new HarmonyMethod( typeof( TypeResolution ), "FindInternalType" ) );
            }
            catch ( Exception e )
            {
                foreach ( MethodBase method in installedMethods )
                {
                    try
                    {
                        harmony.Unpatch( method, HarmonyPatchType.All, harmony.Id );
                    }
                    catch ( Exception )
                    {
                    }
                }
                Main.Log( "Failed to patch Mono.CSharp type lookup (" + e.GetType().Name + ": " + e.Message + ") — " +
                          InternalTypesWarning );
            }
        }

        private const string NumberPatternWarning =
            "in execute_code, a number or enum constant pattern on a value of another type, such as `x is 3` on an object, fails as an internal compiler error";

        private static readonly FieldInfo PatternHelperField =
            typeof( ModuleContainer ).GetField( "pmh", BindingFlags.Instance | BindingFlags.NonPublic );

        private static readonly FieldInfo ContainersField =
            typeof( TypeContainer ).GetField( "containers", BindingFlags.Instance | BindingFlags.NonPublic );

        private const string InternalSignatureWarning =
            "another assembly's internal type cannot be a base class or appear in an internal member's signature (CS0060, CS0051)";

        private static int accessCheckDepth;

        private const string InternalTypesWarning = "a top-level internal type of a loaded assembly cannot be named (CS0122)";

        private const string UnresolvedIsWarning =
            "`is` naming a type that does not exist fails as an internal compiler error instead of naming the type";

        private static void FindInternalType( Namespace __instance, string name, int arity, ref TypeSpec __result )
        {
            if ( __result != null || hideInternal || arity < 0 )
            {
                return;
            }

            Dictionary<string, IList<TypeSpec>> types = NamespaceTypes.GetValue( __instance ) as Dictionary<string, IList<TypeSpec>>;
            IList<TypeSpec> found;
            if ( types == null || !types.TryGetValue( name, out found ) )
            {
                return;
            }

            foreach ( TypeSpec ts in found )
            {
                if ( ts.Arity == arity && ts.Kind != MemberKind.MissingType )
                {
                    __result = ts;
                    return;
                }
            }
        }

        // A simple name looks in the enclosing namespaces before the usings, so an internal type there would shadow a public one a using brings in.
        private static void BeginSimpleLookup( out LookupState __state )
        {
            __state = new LookupState { Hidden = hideInternal, Retry = retryPending, Retrying = retrying };
            hideInternal = !retryPending;
            retrying = retryPending;
            retryPending = false;
        }

        private static void RetrySimpleLookup( NamespaceContainer __instance, string name, int arity, LookupMode mode, Location loc,
            ref FullNamedExpression __result, LookupState __state )
        {
            hideInternal = __state.Hidden;
            retrying = __state.Retrying;
            if ( __result != null || __state.Retry )
            {
                return;
            }

            retryPending = true;
            __result = __instance.LookupNamespaceOrType( name, arity, mode, loc );
        }

        // An alias or using static can be defined lazily inside a simple-name lookup, and its target is a lookup of its own.
        private static void BeginUsingDefine( out LookupState __state )
        {
            __state = new LookupState { Hidden = hideInternal, Retrying = retrying };
            hideInternal = false;
            retrying = false;
            retryPending = false;
        }

        private static Exception EndLookupScope( Exception __exception, LookupState __state )
        {
            hideInternal = __state.Hidden;
            retrying = __state.Retrying;
            retryPending = false;
            return __exception;
        }

        // mcs keeps the later of two inaccessible types a usings search finds; once they can be named, that is an ambiguity.
        private static bool AmbiguousInternalTypes( ModuleContainer module, TypeSpec ts, TypeSpec found, ref TypeSpec __result )
        {
            if ( !retrying || ts == found || NameableAsBefore( module, ts ) || NameableAsBefore( module, found ) )
            {
                return true;
            }

            __result = null;
            return false;
        }

        private static bool NameableAsBefore( ModuleContainer module, TypeSpec type )
        {
            return ( type.Modifiers & Modifiers.PUBLIC ) != 0 || type.MemberDefinition.IsInternalAsPublic( module.DeclaringAssembly );
        }

        // The consistency check lets an internal member use an internal type of its own assembly only; another assembly's now counts the same.
        private static void BeginAccessCheck()
        {
            accessCheckDepth++;
        }

        private static Exception EndAccessCheck( Exception __exception )
        {
            accessCheckDepth--;
            return __exception;
        }

        private static bool InternalAsOwn( ref bool __result )
        {
            if ( accessCheckDepth == 0 )
            {
                return true;
            }

            __result = true;
            return false;
        }

        private static void KeepProbeType( Is __instance, out Expression __state )
        {
            Expression operand = __instance.ProbeType;
            // mcs's pattern classes don't implement CloneTo, so Clone() throws for them.
            // Resolving the operand as an expression can replace its parts, so the failure is re-resolved from a copy taken first.
            __state = operand == null || operand is TypeExpr || IsPattern( operand ) ? operand : operand.Clone( new CloneContext() );
        }

        private static bool IsPattern( Expression operand )
        {
            for ( Type type = operand.GetType(); type != null; type = type.BaseType )
            {
                if ( type.FullName == "Mono.CSharp.PatternExpression" )
                {
                    return true;
                }
            }

            return false;
        }

        // When the operand resolves neither as a type nor as a pattern, mcs merges both readings' errors into a printer it discards and goes on with a null or error operand.
        private static void ReportUnresolvedProbeType( Is __instance, ResolveContext rc, ref TypeSpec ___probe_type_expr,
            Expression __state )
        {
            if ( ___probe_type_expr != null || __state == null ||
                 ( __instance.ProbeType != null && !( __instance.ProbeType is ErrorExpression ) ) )
            {
                return;
            }

            __instance.ProbeType = null;
            MemberAccess access = __state as MemberAccess;
            if ( access != null && NamesType( access.LeftExpression.Clone( new CloneContext() ), rc ) )
            {
                Expression value = __state.Resolve( rc );
                if ( value != null && !( value is ErrorExpression ) )
                {
                    __instance.ProbeType = value;
                }
                return;
            }

            TypeSpec type = __state.ResolveAsType( rc );
            if ( type != null )
            {
                __instance.ProbeType = __state;
                ___probe_type_expr = type;
            }
        }

        private static bool NamesType( Expression expression, ResolveContext rc )
        {
            FullNamedExpression name = expression as FullNamedExpression;
            if ( name == null )
            {
                return false;
            }

            ReportPrinter previous = rc.Report.SetPrinter( new ProbePrinter() );
            try
            {
                return name.ResolveAsTypeOrNamespace( rc, false ) is TypeExpr;
            }
            finally
            {
                rc.Report.SetPrinter( previous );
            }
        }

        private sealed class ProbePrinter : ReportPrinter
        {
        }

        private static bool SkipFailedProbe( Is __instance, ref Expression __result )
        {
            if ( __instance.ProbeType != null )
            {
                return true;
            }

            __result = null;
            return false;
        }

        // The Evaluator's module outlives each compile, and a helper built into one compile's assembly, or left half-emitted by a failed one, cannot be emitted again.
        private static void DropPatternHelper( ModuleContainer ___module )
        {
            TypeContainer helper = PatternHelperField.GetValue( ___module ) as TypeContainer;
            if ( helper == null )
            {
                return;
            }

            PatternHelperField.SetValue( ___module, null );
            List<TypeContainer> containers = ContainersField.GetValue( ___module ) as List<TypeContainer>;
            if ( containers != null )
            {
                containers.Remove( helper );
            }
        }

        // The Evaluator resolves its host method before the module's PrepareEmit pass, so the pattern helper a body creates, which prepares itself, is prepared again there.
        private static bool SkipPreparedMatcher( ClassOrStruct __instance )
        {
            ModuleContainer.PatternMatchingHelper helper = __instance as ModuleContainer.PatternMatchingHelper;
            return helper == null || helper.NumberMatcher == null || helper.NumberMatcher.MethodBuilder == null;
        }
    }
}
