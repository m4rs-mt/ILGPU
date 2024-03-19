// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2023-2024 ILGPU Project
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
using System.IO;
using System.Threading.Tasks;
using VerifyTests;
using VerifyXunit;

namespace ILGPU.Analyzers.Tests.Generic
{
    public static class IncrementalGeneratorVerifier<TIncrementalGenerator>
        where TIncrementalGenerator : IIncrementalGenerator, new()
    {
        public static Task Verify(string source)
        {
            var ilgpuAssemblies =
                new[]
                {
                    typeof(InterleaveFieldsAttribute).Assembly,
                    typeof(TIncrementalGenerator).Assembly
                };

            var compilation =
                SourceCompiler.CreateCompilationWithAssemblies("Tests", source,
                    ilgpuAssemblies);

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