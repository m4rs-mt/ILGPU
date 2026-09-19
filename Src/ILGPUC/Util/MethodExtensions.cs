// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPUC.Util;

/// <summary>
/// Extensions for methods.
/// </summary>
static class MethodExtensions
{
    /// <summary>
    /// The maximum nesting depth of the type arguments of a method kernels call
    /// (see <see cref="GetTypeArgumentNesting"/>).
    /// </summary>
    public const int MaxTypeArgumentNesting = 32;

    /// <summary>
    /// Returns how deeply the type arguments of the given method and its
    /// declaring type are nested: zero without type arguments that are generic
    /// types, arrays or pointers, and one more for each level of them. A generic
    /// method calling itself with a type argument that wraps its own, such as
    /// <c>F&lt;Wrap&lt;T&gt;&gt;</c> in <c>F&lt;T&gt;</c>, creates instantiations
    /// that are nested ever more deeply, while there are finitely many
    /// instantiations of a bounded nesting depth.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>The nesting depth of the type arguments.</returns>
    public static int GetTypeArgumentNesting(this MethodBase method)
    {
        int nesting = GetNesting(method.DeclaringType) - 1;
        if (method.IsGenericMethod)
        {
            foreach (var argument in method.GetGenericArguments())
                nesting = Math.Max(nesting, GetNesting(argument));
        }
        return Math.Max(nesting, 0);

        // One more than the nesting of the type arguments or the element type,
        // or zero for other types
        static int GetNesting(Type? type)
        {
            if (type is null)
                return 0;
            if (type.HasElementType)
                return 1 + GetNesting(type.GetElementType());
            if (!type.IsGenericType)
                return 0;
            int nesting = 0;
            foreach (var argument in type.GetGenericArguments())
                nesting = Math.Max(nesting, GetNesting(argument));
            return 1 + nesting;
        }
    }

    /// <summary>
    /// Returns a parameter offset of 1 for instance methods and 0 for static
    /// methods.
    /// </summary>
    /// <param name="method">The method to compute the parameter offset for.</param>
    /// <returns>
    /// A parameter offset of 1 for instance methods and 0 for static methods.
    /// </returns>
    public static int GetParameterOffset(this MethodBase method) =>
        method.IsStatic || method.IsNotCapturingLambda() ? 0 : 1;

    /// <summary>
    /// Returns the name of the given method as written in source code, which
    /// diagnostics refer to: the name of a local function, or the name of the
    /// method containing a lambda or an inline kernel.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>The display name of the method.</returns>
    public static string GetDisplayName(this MethodBase method)
    {
        // Compiler-generated names have the form '<Outer>k__Inner|...'
        var name = method.Name;
        int end = name.IndexOf('>', StringComparison.Ordinal);
        if (!name.StartsWith('<') || end < 1)
            return name;
        var outer = name[1..end];
        if (string.CompareOrdinal(name, end + 1, "g__", 0, 3) == 0)
        {
            var inner = name[(end + 4)..];
            int separator = inner.IndexOf('|', StringComparison.Ordinal);
            if (separator >= 0)
                inner = inner[..separator];
            if (!inner.StartsWith("InlineKernel_", StringComparison.Ordinal))
                return inner;
        }
        return outer.Length > 0 ? outer : name;
    }

    /// <summary>
    /// Returns the number of parameters including the main parameter offset.
    /// methods.
    /// </summary>
    /// <param name="method">The method to compute the parameter offset for.</param>
    /// <returns>
    /// The total number of parameters including the
    /// </returns>
    public static int GetNumParametersIncludingOffset(this MethodBase method) =>
        method.GetParameterOffset() + method.GetParameters().Length;

    /// <summary>
    /// The full name of the F# attribute that marks closure classes.
    /// </summary>
    private const string FSharpCompilationMappingAttribute =
        "Microsoft.FSharp.Core.CompilationMappingAttribute";

    /// <summary>
    /// The value of <c>SourceConstructFlags.Closure</c> in F#.
    /// </summary>
    private const int FSharpSourceConstructClosure = 6;

    /// <summary>
    /// The value of <c>SourceConstructFlags.KindMask</c> in F#.
    /// </summary>
    private const int FSharpSourceConstructKindMask = 31;

    /// <summary>
    /// Returns true if the method can be considered a non-capturing lambda.
    /// </summary>
    /// <param name="method">The method to check.</param>
    /// <returns>True, if the method is a non-capturing lambda.</returns>
    public static bool IsNotCapturingLambda(this MethodBase method)
    {
        if (method.IsStatic)
            return false;

        // C# and F# both create a helper class to represent a lambda - the fields of
        // the class are used to capture the variables. To detect a non-capturing
        // lambda, we look for a compiler-generated closure class that has no
        // instance fields or properties (so that it cannot have local state).
        // Ordinary classes without instance state (e.g. System.Object) must not
        // match: calls to their instance methods take 'this' from the stack.
        //
        // IMPORTANT: It is possible for the lambda to capture static fields and
        // properties, and still pass this detection because we do not inspect the
        // IL instructions here. We are relying on the rest of the compilation to
        // detect invalid cases.
        //
        // NB: In future, this will only apply to C#, as F# has been updated to create
        // a static function for non-capturing lambdas:
        // https://github.com/dotnet/fsharp/tree/596f3d7
        //
        var declaringType = method.DeclaringType;
        return declaringType != null
            && declaringType.IsClass
            && IsCompilerGeneratedClosureType(declaringType)
            && declaringType.GetFields(
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public).Length == 0
            && declaringType.GetProperties(
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public).Length == 0;
    }

    /// <summary>
    /// Returns true if the given type is a sealed closure class generated by the
    /// C#, Visual Basic or F# compiler.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True, if the type is a compiler-generated closure class.</returns>
    private static bool IsCompilerGeneratedClosureType(Type type)
    {
        if (!type.IsSealed)
            return false;

        // C# emits '<>c' (non-capturing lambdas) and '<>c__DisplayClass*', Visual
        // Basic emits '_Closure$__*', both marked with [CompilerGenerated]
        if (type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
        {
            return type.Name.StartsWith("<>c", StringComparison.Ordinal)
                || type.Name.StartsWith("_Closure$__", StringComparison.Ordinal);
        }

        // F# marks closures with [CompilationMapping(SourceConstructFlags.Closure)]
        foreach (var attribute in type.CustomAttributes)
        {
            if (attribute.AttributeType.FullName != FSharpCompilationMappingAttribute
                || attribute.ConstructorArguments.Count < 1
                || attribute.ConstructorArguments[0].Value is not int flags)
            {
                continue;
            }
            if ((flags & FSharpSourceConstructKindMask) == FSharpSourceConstructClosure)
                return true;
        }
        return false;
    }
}
