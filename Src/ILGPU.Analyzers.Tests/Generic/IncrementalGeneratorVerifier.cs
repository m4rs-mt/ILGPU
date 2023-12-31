// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: IncrementalGeneratorVerifier.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VerifyTests;
using VerifyXunit;

namespace ILGPU.Analyzers.Tests
{
    public static class IncrementalGeneratorVerifier<TIncrementalGenerator>
        where TIncrementalGenerator : IIncrementalGenerator, new()
    {
        public static Task Verify(string source)
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

            var ilgpuReferences =
                new[]
                {
                    typeof(InterleaveFieldsAttribute),
                    typeof(TIncrementalGenerator)
                }
                .Select(x => MetadataReference.CreateFromFile(x.Assembly.Location))
                .ToArray();

            // Create a roslyn compilation for the syntax tree.
            var compilation = CSharpCompilation.Create(
                "Tests",
                new[] { syntaxTree },
                references: systemReferences.Concat(ilgpuReferences));

            // Create an instance of the incremental source generator.
            var generator = new TIncrementalGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

            // Run generator and compare to snapshot.
            driver = driver.RunGenerators(compilation);

            var settings = new VerifySettings();
            settings.UseDirectory(Path.Combine("..", "Snapshots"));
            return Verifier.Verify(driver, settings);
        }
    }
}
