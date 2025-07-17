// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: InterleaveFields.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Threading.Tasks;
using Xunit;
using VerifyCS =
    ILGPU.Analyzers.Tests.Generic.IncrementalGeneratorVerifier<
        ILGPU.Analyzers.InterleaveFieldsGenerator>;

namespace ILGPU.Analyzers.Tests
{
    public class InterleaveFields
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

[InterleaveFields(typeof(MyPoint), 4)]
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

[InterleaveFields(typeof(MyPoint), 4)]
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

    [InterleaveFields(typeof(MyPoint), 4)]
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

        [InterleaveFields(typeof(MyPoint), 4)]
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

        [InterleaveFields(typeof(MyPoint), 4)]
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

[InterleaveFields(typeof(MyPoint), 4)]
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

[InterleaveFields(typeof(MyPoint), 4)]
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

[InterleaveFields(typeof(MyPoint), 4)]
partial struct MyPoint4
{ }
";
            await VerifyCS.Verify(code);
        }

    }
}
