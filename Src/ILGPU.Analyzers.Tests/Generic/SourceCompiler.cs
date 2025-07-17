// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: SourceCompiler.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ILGPU.Analyzers.Tests.Generic;

/// <summary>
/// Contains utility functions for compiling C# source files for analyzer testing.
/// </summary>
public static class SourceCompiler
{
    /// <summary>
    /// Compiles the source text <c>source</c> into an assembly with the name
    /// <c>assemblyName</c> and includes the given <c>additionalAssemblies</c>.
    /// </summary>
    /// <param name="assemblyName">The name of the output assembly.</param>
    /// <param name="source">The source text to compile.</param>
    /// <param name="additionalAssemblies">
    /// The additional assembly references to include in the compilation.
    /// </param>
    /// <returns>The resulting compilation.</returns>
    public static CSharpCompilation CreateCompilationWithAssemblies(
        string assemblyName,
        string source,
        Assembly[] additionalAssemblies)
    {
        // Parse syntax tree.
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Add system references.
        var trustedAssembliesPaths =
            (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        var systemReferences =
            trustedAssembliesPaths!
                .Split(Path.PathSeparator)
                .Select(x => MetadataReference.CreateFromFile(x))
                .ToArray();

        var additionalReferences =
            additionalAssemblies
                .Select(x => MetadataReference.CreateFromFile(x.Location))
                .ToArray();

        // Create a roslyn compilation for the syntax tree.
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references: systemReferences.Concat(additionalReferences));

        return compilation;
    }
}