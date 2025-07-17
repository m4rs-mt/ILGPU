// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: ManagedTypeAnalyzer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.IO;
using System.Threading.Tasks;
using Xunit;
using VerifyCS =
    ILGPU.Analyzers.Tests.Generic.DiagnosticAnalyzerVerifier<
        ILGPU.Analyzers.ManagedTypeAnalyzer>;

namespace ILGPU.Analyzers.Tests;

public class ManagedTypeAnalyzer
{
    [Theory]
    [InlineData("Simple")]
    [InlineData("Complex")]
    [InlineData("Arrays")]
    [InlineData("Functions")]
    [InlineData("PartialMethods")]
    [InlineData("Constructors")]
    [InlineData("LoadDiscovery")]
    [InlineData("ILGPUTypesIntrinsics")]
    public async Task FileTests(string file)
    {
        // In build, we copy all programs to output directory.
        // See ILGPU.Analyzers.Tests.csproj
        var code = await File.ReadAllTextAsync(
            $"Programs/ManagedType/{file}.cs"
        );
        await VerifyCS.Verify(code, settings => settings.UseParameters(file));
    }
}