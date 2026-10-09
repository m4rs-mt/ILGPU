// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.CommandLine;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ILGPUC;

/// <summary>
/// Main ILGPUC program class.
/// </summary>
sealed partial class Program
{
    /// <summary>
    /// Main entry point of the ILGPUC command line compiler.
    /// </summary>
    /// <param name="args">The command line args passed to ILGPUC.</param>
    static Task<int> Main(string[] args)
    {
        // Build the root command
        var root = new RootCommand("ILGPUC command line compiler");
        var _ = new CompileCommand(root);
        var __ = new BuildCommand(root);
        var ___ = new ProbeServicesCommand(root);

        // Parse and invoke with cancellation support
        var parseResult = root.Parse(args);
        return parseResult.InvokeAsync();
    }

    #region Helpers

    /// <summary>
    /// Resolves generic methods via packed command line arguments.
    /// </summary>
    /// <param name="assembly">The source assembly.</param>
    /// <param name="fullyQualifiedMethodName">The fully qualified method name.</param>
    /// <returns>The resolved method info object for processing.</returns>
    public static MethodInfo ResolveMethod(
        Assembly assembly,
        string fullyQualifiedMethodName)
    {
        // Step 1: Normalize input names
        string normalizedMethodName = KernelNameNormalizationRegex()
            .Replace(fullyQualifiedMethodName, match =>
                match.Value switch
                {
                    "__LT__" => "<",
                    "__GT__" => ">",
                    _ => match.Value
                });

        // Step 1: Split into type and method
        int lastDot = normalizedMethodName.LastIndexOf('.');
        if (lastDot == -1)
            throw new ArgumentException("Invalid method name");

        string typeName = normalizedMethodName[..lastDot];
        string methodName = normalizedMethodName[(lastDot + 1)..];

        // Step 2: Get the Type
        var type = assembly.GetType(typeName, throwOnError: true).ThrowIfNull();

        // Step 3: Match method name (possibly generic)
        // Note: MethodInfo.Name does NOT include arity like `1
        var genericSplits = methodName.Split('`', 2);
        string baseMethodName = genericSplits[0];

        var candidates = type.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance)
            .Where(m => m.Name == baseMethodName);

        if (genericSplits.Length > 1 &&
            int.TryParse(genericSplits[1], out int genericArity))
        {
            candidates = candidates.Where(
                m => m.IsGenericMethodDefinition &&
                m.GetGenericArguments().Length == genericArity);
        }

        return candidates.First();
    }

    /// <summary>
    /// Returns a compiled regular expression that matches the encoded angle-bracket
    /// tokens (<c>__LT__</c> and <c>__GT__</c>) used in CLI-safe kernel names.
    /// </summary>
    [GeneratedRegex("__LT__|__GT__")]
    private static partial Regex KernelNameNormalizationRegex();

    #endregion
}
