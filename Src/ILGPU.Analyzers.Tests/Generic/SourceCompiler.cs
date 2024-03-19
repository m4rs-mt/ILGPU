using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ILGPU.Analyzers.Tests.Generic;

public class SourceCompiler
{
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
            trustedAssembliesPaths
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