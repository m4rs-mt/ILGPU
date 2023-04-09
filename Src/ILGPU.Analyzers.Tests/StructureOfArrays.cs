// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructureOfArrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// [ModuleInitializer] is only available from net5.0 and later.
#if NET5_0_OR_GREATER

using System.Threading.Tasks;
using VerifyXunit;
using Xunit;
using VerifyCS =
    ILGPU.Analyzers.Tests.IncrementalGeneratorVerifier<ILGPU.Analyzers.SourceGenerator>;

namespace ILGPU.Analyzers.Tests
{
    [UsesVerify]
    public class StructureOfArrays
    {
        [Fact]
        public async Task Simple()
        {
            var code = @"
using ILGPU.CodeGeneration;

public struct MyPoint
{
    public int X;
    public int Y;
}

[GeneratedStructureOfArrays(typeof(MyPoint), 4)]
partial struct MyPoint4
{ }
";
            await VerifyCS.Verify(code);
        }

        [Fact]
        public async Task SimpleNotPartial()
        {
            var code = @"
using ILGPU.CodeGeneration;

public struct MyPoint
{
    public int X;
    public int Y;
}

[GeneratedStructureOfArrays(typeof(MyPoint), 4)]
struct MyPoint4
{ }
";
            await VerifyCS.Verify(code);
        }

        [Fact]
        public async Task NestedNamespace()
        {
            var code = @"
using ILGPU.CodeGeneration;

namespace Alpha.Beta
{
    public struct MyPoint
    {
        public int X;
        public int Y;
    }

    [GeneratedStructureOfArrays(typeof(MyPoint), 4)]
    partial struct MyPoint4
    { }
}
";
            await VerifyCS.Verify(code);
        }

        [Fact]
        public async Task NestedNamespaceClass()
        {
            var code = @"
using ILGPU.CodeGeneration;

namespace Alpha.Beta
{
    partial class Gamma
    {
        public struct MyPoint
        {
            public int X;
            public int Y;
        }

        [GeneratedStructureOfArrays(typeof(MyPoint), 4)]
        partial struct MyPoint4
        { }
    }
}
";
            await VerifyCS.Verify(code);
        }

        [Fact]
        public async Task NestedNamespaceClassNotPartial()
        {
            var code = @"
using ILGPU.CodeGeneration;

namespace Alpha.Beta
{
    class Gamma
    {
        public struct MyPoint
        {
            public int X;
            public int Y;
        }

        [GeneratedStructureOfArrays(typeof(MyPoint), 4)]
        partial struct MyPoint4
        { }
    }
}
";
            await VerifyCS.Verify(code);
        }

        [Fact]
        public async Task FlattenStruct()
        {
            var code = @"
using ILGPU.CodeGeneration;

public struct MyPoint
{
    public int X;
    public int Y;
    public InnerStruct Z;
}

public struct InnerStruct
{
    public int A;
    public int B;
}

[GeneratedStructureOfArrays(typeof(MyPoint), 4)]
partial struct MyPoint4
{ }
";
            await VerifyCS.Verify(code);
        }

        [Fact]
        public async Task FlattenStructFixedSizeBuffers()
        {
            var code = @"
using ILGPU.CodeGeneration;

public struct MyPoint
{
    public int X;
    public int Y;
    public InnerStruct Z;
}

public unsafe struct InnerStruct
{
    public fixed int A[4];
    public fixed int B[2];
}

[GeneratedStructureOfArrays(typeof(MyPoint), 4)]
partial struct MyPoint4
{ }
";
            await VerifyCS.Verify(code);
        }

        [Fact]
        public async Task Accessibility()
        {
            var code = @"
using ILGPU.CodeGeneration;

public struct MyPoint
{
    internal int X;
    private int Y;
}

[GeneratedStructureOfArrays(typeof(MyPoint), 4)]
partial struct MyPoint4
{ }
";
            await VerifyCS.Verify(code);
        }

    }
}

#endif
