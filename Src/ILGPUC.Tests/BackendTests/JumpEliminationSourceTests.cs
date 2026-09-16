// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: JumpEliminationSourceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using Group = ILGPU.Group;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Checks that the source backends express jumps by nesting, single breaks
/// and continues, and duplicated small blocks instead of the jump variable
/// and breakable regions, while barriers, shuffles and calls stay in a single
/// place.
/// </summary>
public sealed class JumpEliminationSourceTests
{
    #region Kernels

    /// <summary>
    /// Selects a value in a switch whose cases share bodies.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Select(int x)
    {
        int r = x * 2;
        switch (x)
        {
            case 0:
                return 11;
            case 2:
            case 4:
                r = x * 7 + 1;
                break;
            case 1:
            case 5:
                break;
            case 6:
                if (x > 20)
                    return 3;
                break;
            case 7:
                return r - 40;
        }
        return r + 100;
    }

    /// <summary>
    /// Calls a switch whose cases share bodies (shared switch edges).
    /// </summary>
    internal static void SwitchSharedCasesKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Select(index.X % 9);
    }

    /// <summary>
    /// Leaves a loop through a break or its condition into the same code
    /// (empty forwarding blocks).
    /// </summary>
    internal static void LoopBreakKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        for (int i = 0; i < 100; i++)
        {
            sum += i;
            if (sum > 10 + index.X % 9)
                break;
        }
        data[index] = sum;
    }

    /// <summary>
    /// Leaves an inner loop through two exits with different values that
    /// merge before the latch of the outer loop (loop exits).
    /// </summary>
    internal static void InnerLoopTwoExitsKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> data)
    {
        long r = 0;
        for (int i = 0; i < 4; i++)
        {
            long s = 0;
            int j;
            for (j = 0; j < 4; j++)
            {
                if (i + j > index.X % 9)
                    break;
                s += j + 1;
            }
            long t = j < 4 ? s : s + 100;
            r += t * (i + 1);
        }
        data[index] = r;
    }

    /// <summary>
    /// Adds to a counter only if the index is large enough, and divides by the
    /// result only if it is not zero (short-circuit conditions).
    /// </summary>
    internal static void GuardedDivisionKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> counter)
    {
        int b;
        if (index.X > 1 && (b = Atomic.Add(ref counter[0], 1)) != 0 && 100 / b > 2)
            data[index] = 1;
        else
            data[index] = 0;
    }

    /// <summary>
    /// Continues a loop from a switch case, and leaves it from another one
    /// (loop latch).
    /// </summary>
    internal static void SwitchContinueKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> input)
    {
        int s = index.X;
        for (int i = 0; i < 12; i++)
        {
            switch (input[i + index.X] % 4)
            {
                case 0:
                    s += 1;
                    continue;
                case 1:
                    s += 10;
                    break;
                case 2:
                    if (s > 900)
                        goto done;
                    s += 100;
                    break;
                default:
                    s -= 1;
                    break;
            }
            s = s * 2 % 1009;
        }
    done:
        data[index] = s;
    }

    /// <summary>
    /// Continues in other switch cases through goto case (small merge
    /// blocks).
    /// </summary>
    internal static void SwitchGotoCaseKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> data)
    {
        int x = index.X % 9;
        int y = x * 3;
        int r;
        switch (x - 1)
        {
            case 0:
                r = 5;
                goto case 1;
            case 1:
                r = y;
                r += 7;
                break;
            case 2:
                r = 1;
                goto default;
            default:
                r = y + 100;
                break;
        }
        data[index] = r;
    }

    /// <summary>
    /// Leaves three nested loops through a goto to a store and return (small
    /// return blocks).
    /// </summary>
    internal static void TripleLoopGotoKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = index.X % 9;
        int r = 0;
        for (int a = 0; a < 3; a++)
        {
            for (int b = 0; b < 3; b++)
            {
                for (int c = 0; c < 3; c++)
                {
                    r += a * 9 + b * 3 + c;
                    if (r > x * 12 + 5)
                        goto done;
                }
                r ^= b;
            }
            r += 7;
        }
        r = -r;
    done:
        data[index] = r * 10 + x;
    }

    /// <summary>
    /// Leaves three nested loops through a goto to the end of the outer loop
    /// body, which needs the jump variable.
    /// </summary>
    internal static void NestedLoopGotoNextKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int a, b = 0, c = 0, r = 0;
        for (a = 0; a < 4; a++)
        {
            for (b = 0; b < 4; b++)
            {
                for (c = 0; c < 4; c++)
                {
                    int v = input[(a * 16 + b * 4 + c + index.X * 5) % 64];
                    if (v == 13)
                        goto done;
                    if (v > 40)
                        goto nextA;
                    if (v < -40)
                        break;
                    r += v;
                }
                r ^= b;
            }
        nextA:
            r += 1000;
        }
    done:
        data[index] = r * 7 + a * 100 + b * 10 + c;
    }

    /// <summary>
    /// Returns the value of a lane-dependent loop exit.
    /// </summary>
    private static int SearchExit(int x)
    {
        int r = 0;
        for (int i = 0; i < 8; i++)
        {
            int v = (i * 5 + x * 3) % 11;
            if (v > 8)
                return i * 3 + x;
            r += v;
        }
        return r * 2 + 1;
    }

    /// <summary>
    /// Leaves a loop in different iterations into a merge block with a group
    /// barrier.
    /// </summary>
    internal static void MergeWithBarrierKernel(ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(64);
        int g = Group.Index;
        int t = (int)Grid.GlobalThreadIndex;
        int x = t % 9;
        int r = 0;
        for (int i = 0; i < 8; i++)
        {
            int v = (i * 5 + x * 3) % 11;
            if (v > 8)
            {
                r = i * 3 + x;
                goto merge;
            }
            r += v;
        }
        r = r * 2 + 1;
    merge:
        shared[g] = r;
        Group.Barrier();
        data[t] = r * 1000 + shared[g ^ 1];
    }

    /// <summary>
    /// Leaves a loop in different iterations into a merge block with a warp
    /// shuffle.
    /// </summary>
    internal static void MergeWithShuffleKernel(ArrayView1D<int, Stride1D.Dense> data)
    {
        int t = (int)Grid.GlobalThreadIndex;
        int x = t % 9;
        int r = 0;
        for (int i = 0; i < 8; i++)
        {
            int v = (i * 5 + x * 3) % 11;
            if (v > 8)
            {
                r = i * 3 + x;
                goto merge;
            }
            r += v;
        }
        r = r * 2 + 1;
    merge:
        int other = Warp.ShuffleXor(r, 1);
        data[t] = r * 1000 + other;
    }

    /// <summary>
    /// Adjusts a value in a call that is never inlined.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Adjust(int value) => value * 3 + 1;

    /// <summary>
    /// Leaves a loop in different iterations into a merge block with a call.
    /// </summary>
    internal static void MergeWithCallKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = index.X % 9;
        int r = 0;
        for (int i = 0; i < 8; i++)
        {
            int v = (i * 5 + x * 3) % 11;
            if (v > 8)
            {
                r = i * 3 + x;
                goto merge;
            }
            r += v;
        }
        r = r * 2 + 1;
    merge:
        data[index] = Adjust(r);
    }

    /// <summary>
    /// Evaluates a chain of small diamonds with loads, which needs no
    /// duplication.
    /// </summary>
    internal static void DiamondChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int a = 0;
        if (x > 1) a += data[32]; else a -= 3;
        if (x > 2) a += data[33]; else a -= 5;
        if (x > 3) a += data[34]; else a -= 7;
        if (x > 4) a += data[35]; else a -= 11;
        if (x > 5) a += data[36]; else a -= 13;
        if (x > 6) a += data[37]; else a -= 17;
        if (x > 7) a += data[38]; else a -= 19;
        if (x > 8) a += data[39]; else a -= 23;
        if (x > 9) a += data[40]; else a -= 29;
        if (x > 10) a += data[41]; else a -= 31;
        if (x > 11) a += data[42]; else a -= 37;
        if (x > 12) a += data[43]; else a -= 41;
        if (x > 13) a += data[44]; else a -= 43;
        if (x > 14) a += data[45]; else a -= 47;
        if (x > 15) a += data[46]; else a -= 53;
        if (x > 16) a += data[47]; else a -= 59;
        if (x > 17) a += data[48]; else a -= 61;
        if (x > 18) a += data[49]; else a -= 67;
        if (x > 19) a += data[50]; else a -= 71;
        if (x > 20) a += data[51]; else a -= 73;
        data[index] = a;
    }

    #endregion

    private static string Generate(string kernelName, BackendType backend)
    {
        var kernel = typeof(JumpEliminationSourceTests).GetMethod(
            kernelName,
            BindingFlags.NonPublic | BindingFlags.Static)!;
        using var helper = new CompilationHelper(backend);
        return helper.GenerateBackendCode(kernel, backend).SourceCode;
    }

    private static int Count(string source, string pattern) =>
        Regex.Matches(source, pattern, RegexOptions.Multiline).Count;

    private const string JumpVariable = @"\b_jump\d+\b";
    private const string Region = @"while\s*\(\s*false\s*\)";
    private const string Return = @"^\s*return\b[^;]*;";
    private const string Continue = @"\bcontinue\s*;";
    private const string BarrierStatement =
        @"^\s*(__syncthreads\(\)|threadgroup_barrier\(mem_flags::mem_threadgroup\)"
        + @"|barrier\(CLK_LOCAL_MEM_FENCE \| CLK_GLOBAL_MEM_FENCE\));";
    private const string Shuffle =
        @"\b(__shfl_xor_sync|__shfl_xor|simd_shuffle_xor|(intel_)?sub_group_shuffle_xor)"
        + @"\s*\(";

    /// <summary>
    /// Returns the code of the kernel entry point, which follows the helper
    /// functions.
    /// </summary>
    private static string GetEntryPoint(string source) =>
        source[Regex.Matches(source, @"__global__|__kernel|\bkernel void")[^1].Index..];

    private static void AssertNoJumps(string source)
    {
        Assert.DoesNotMatch(JumpVariable, source);
        Assert.DoesNotMatch(Region, source);
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void SwitchSharedCases_HasNoJumps(BackendType backend)
    {
        var source = Generate(nameof(SwitchSharedCasesKernel), backend);
        AssertNoJumps(source);
        Assert.Equal(1, Count(source, @"\bswitch\s*\("));
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void LoopBreak_HasNoJumps(BackendType backend)
    {
        var source = Generate(nameof(LoopBreakKernel), backend);
        AssertNoJumps(source);
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void InnerLoopTwoExits_HasNoJumps(BackendType backend)
    {
        var source = Generate(nameof(InnerLoopTwoExitsKernel), backend);
        AssertNoJumps(source);
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void GuardedDivision_DividesOnceUnderItsGuard(BackendType backend)
    {
        var source = Generate(nameof(GuardedDivisionKernel), backend);
        AssertNoJumps(source);
        Assert.Equal(1, Count(source, @"\b100\s*/"));
        Assert.Equal(1, Count(GetEntryPoint(source), @"\b(atomic\w*|atom_\w+)\s*\("));
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void SwitchContinue_ContinuesFromTheCase(BackendType backend)
    {
        var source = Generate(nameof(SwitchContinueKernel), backend);
        AssertNoJumps(source);
        Assert.Equal(1, Count(source, Continue));
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void SwitchGotoCase_HasNoJumps(BackendType backend)
    {
        var source = Generate(nameof(SwitchGotoCaseKernel), backend);
        AssertNoJumps(source);
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void TripleLoopGoto_ReturnsFromTheInnerLoop(BackendType backend)
    {
        var source = Generate(nameof(TripleLoopGotoKernel), backend);
        AssertNoJumps(source);
        Assert.Equal(2, Count(GetEntryPoint(source), Return));
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void NestedLoopGotoNext_KeepsOneJumpTarget(BackendType backend)
    {
        var source = Generate(nameof(NestedLoopGotoNextKernel), backend);
        var ids = Regex.Matches(source, @"\b_jump\d+\s*=\s*([1-9]\d*)\s*;")
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToList();
        Assert.Single(ids);
        Assert.DoesNotMatch(Region, source);
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void MergeWithBarrier_EmitsTheBarrierOnce(BackendType backend)
    {
        var source = Generate(nameof(MergeWithBarrierKernel), backend);
        Assert.Equal(1, Count(source, BarrierStatement));
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void MergeWithShuffle_EmitsTheShuffleOnce(BackendType backend)
    {
        var source = Generate(nameof(MergeWithShuffleKernel), backend);
        Assert.Equal(1, Count(source, Shuffle));
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void MergeWithCall_EmitsTheCallOnce(BackendType backend)
    {
        var source = Generate(nameof(MergeWithCallKernel), backend);
        Assert.Equal(1, Count(source, @"=\s*\w*Adjust\w*\("));
    }

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.Metal)]
    public void DiamondChain_DuplicatesNothing(BackendType backend)
    {
        var source = Generate(nameof(DiamondChainKernel), backend);
        AssertNoJumps(source);
        Assert.Equal(1, Count(GetEntryPoint(source), Return));
        Assert.Equal(20, Count(GetEntryPoint(source), @"^\s*if\s*\(.*\)\s*$"));
    }
}
