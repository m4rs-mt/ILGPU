// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructuredControlFlowSourceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Checks the shape of the structured control flow the source backends emit:
/// loops keep their natural form and local storage is declared once at
/// function scope, whatever construct uses it.
/// </summary>
public sealed class StructuredControlFlowSourceTests
{
    #region Kernels

    private unsafe struct Buffer4
    {
        public fixed int Data[4];
    }

    /// <summary>
    /// Sums a lane-dependent number of elements in a counted loop.
    /// </summary>
    internal static void CountedLoopKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        int n = index.X % 7;
        for (int i = 0; i < n; i++)
            sum += data[i];
        data[index] = sum;
    }

    /// <summary>
    /// Sums the decimal digits of a value in a do-while loop.
    /// </summary>
    internal static void DoWhileKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int v = data[index];
        int sum = 0;
        do
        {
            sum += v % 10;
            v /= 10;
        }
        while (v != 0);
        data[index] = sum;
    }

    /// <summary>
    /// Halves a value in a do-while loop with a two-part condition.
    /// </summary>
    internal static void DoWhileTwoTestsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data)
    {
        float x = data[index];
        int it = 0;
        do
        {
            x = x * 0.5f + 1.0f;
            it++;
        }
        while (x > 2.0f && it < 50);
        data[index] = x + it;
    }

    /// <summary>
    /// Swaps two values in every iteration of a counted loop.
    /// </summary>
    internal static void SwapLoopKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int a = data[0];
        int b = data[1];
        int n = index.X % 5;
        for (int i = 0; i < n; i++)
        {
            int t = a;
            a = b;
            b = t;
        }
        data[index] = a * 10 + b;
    }

    /// <summary>
    /// Adjusts a value in a call that is never inlined.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Adjust(int value) => value * 3 + 1;

    /// <summary>
    /// Reads a fixed-size buffer after a loop whose early exit jumps past the
    /// code after the loop. The code after the loop and the merge block call
    /// a helper, so neither is duplicated and the jump needs a region.
    /// </summary>
    internal static unsafe void BufferAfterRegionKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int lane = index.X;
        Buffer4 buffer;
        buffer.Data[0] = lane;
        buffer.Data[1] = lane * 2;
        buffer.Data[2] = 3;
        buffer.Data[3] = 4;
        int r;
        if (lane % 3 == 0)
            r = 1;
        else
        {
            r = 2;
            for (int i = 0; i < 10; i++)
            {
                if (data[i + lane] > 40)
                {
                    r = 3;
                    goto merge;
                }
                r += 1;
            }
            r = Adjust(r);
        }
    merge:
        data[index] = Adjust(r) * 100 + buffer.Data[lane % 4];
    }

    /// <summary>
    /// Reads a fixed-size buffer in and after a loop with two entry blocks.
    /// </summary>
    private static unsafe int BufferInIrreducibleLoop(int lane)
    {
        Buffer4 buffer;
        buffer.Data[0] = lane;
        buffer.Data[1] = 2 * lane + 1;
        buffer.Data[2] = 3;
        buffer.Data[3] = 4;
        int s = 0, k = 0;
        if ((lane & 1) == 0)
            goto B;
    A:
        s += k + buffer.Data[k & 3];
        k++;
    B:
        s += 2 * k + 1;
        k++;
        if (k < lane % 9)
        {
            if ((k & 2) == 0)
                goto A;
            goto B;
        }
        return s * 10 + buffer.Data[1];
    }

    /// <summary>
    /// Calls a helper with irreducible control flow.
    /// </summary>
    internal static void BufferInIrreducibleLoopKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = BufferInIrreducibleLoop(index.X);
    }

    #endregion

    private static string Generate(string kernelName, BackendType backend)
    {
        var kernel = typeof(StructuredControlFlowSourceTests).GetMethod(
            kernelName,
            BindingFlags.NonPublic | BindingFlags.Static)!;
        using var helper = new CompilationHelper(backend);
        return helper.GenerateBackendCode(kernel, backend).SourceCode;
    }

    private static int Count(string source, string pattern) =>
        Regex.Matches(source, pattern).Count;

    private const string EndlessLoop = @"while\s*\(\s*true\s*\)";
    private const string JumpVariable = @"\b_jump\d+\b";
    private const string StorageDeclaration =
        @"\b\w+_storage(_base)?\s*\[\s*\d+\s*\]\s*(=\s*\{\s*\}\s*)?;";

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void CountedLoop_IsWhileLoop(BackendType backend)
    {
        var source = Generate(nameof(CountedLoopKernel), backend);
        Assert.Matches(@"\bwhile\s*\(", source);
        Assert.DoesNotMatch(EndlessLoop, source);
        Assert.DoesNotMatch(JumpVariable, source);
        Assert.DoesNotMatch(@"\bbreak\s*;", source);
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void DoWhileLoop_IsDoWhileLoop(BackendType backend)
    {
        var source = Generate(nameof(DoWhileKernel), backend);
        Assert.Matches(@"\bdo\s*\{[\s\S]*\}\s*while\s*\(", source);
        Assert.DoesNotMatch(EndlessLoop, source);
        Assert.DoesNotMatch(JumpVariable, source);
        Assert.DoesNotMatch(@"else\s*\{\s*\}", source);
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void DoWhileTwoTests_HasNoEmptyBranch(BackendType backend)
    {
        var source = Generate(nameof(DoWhileTwoTestsKernel), backend);
        Assert.Matches(@"\}\s*while\s*\(", source);
        Assert.DoesNotMatch(EndlessLoop, source);
        Assert.DoesNotMatch(@"else\s*\{\s*\}", source);
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void SwapLoop_UsesPhiTemporary(BackendType backend)
    {
        var source = Generate(nameof(SwapLoopKernel), backend);
        Assert.Matches(@"\b_phi\d+\b", source);
        Assert.DoesNotMatch(EndlessLoop, source);
        Assert.DoesNotMatch(JumpVariable, source);
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void BufferAfterRegion_IsDeclaredAtFunctionScope(BackendType backend)
    {
        var source = Generate(nameof(BufferAfterRegionKernel), backend);
        var region = Regex.Match(source, @"\bdo\s*\{");
        var declaration = Regex.Match(source, StorageDeclaration);
        Assert.True(region.Success, "Expected a breakable region");
        Assert.Equal(1, Count(source, StorageDeclaration));
        Assert.True(declaration.Index < region.Index);
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void BufferInIrreducibleLoop_IsDeclaredAtFunctionScope(BackendType backend)
    {
        var source = Generate(nameof(BufferInIrreducibleLoopKernel), backend);
        var stateMachine = Regex.Match(source, @"\bswitch\s*\(\s*_state\d+\s*\)");
        var declaration = Regex.Match(source, StorageDeclaration);
        Assert.True(stateMachine.Success, "Expected a state machine");
        Assert.Equal(1, Count(source, StorageDeclaration));
        Assert.True(declaration.Index < stateMachine.Index);
    }
}
