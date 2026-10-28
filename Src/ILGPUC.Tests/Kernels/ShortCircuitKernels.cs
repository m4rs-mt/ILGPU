// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ShortCircuitKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions exercising the IfConditionConversion pass: short-circuit
/// branch chains, mixed and/or nesting, internal merge diamonds, multi-exit
/// regions, and the div/rem speculation-safety guards.
/// </summary>
static class ShortCircuitKernels
{
    /// <summary>A three-term and-chain (must fold to a single branch).</summary>
    public static void AndChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b, int c)
    {
        if (a > 0 && b > 0 && c > 0)
            data[index] = 1;
        else
            data[index] = 0;
    }

    /// <summary>A three-term or-chain (must fold to a single branch).</summary>
    public static void OrChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b, int c)
    {
        if (a > 0 || b > 0 || c > 0)
            data[index] = 1;
        else
            data[index] = 0;
    }

    /// <summary>(a || b) && c mixed nesting (must fold).</summary>
    public static void MixedNesting1Kernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b, int c)
    {
        if ((a > 0 || b > 0) && c > 0)
            data[index] = 1;
        else
            data[index] = 0;
    }

    /// <summary>a && (b || c) mixed nesting (must fold).</summary>
    public static void MixedNesting2Kernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b, int c)
    {
        if (a > 0 && (b > 0 || c > 0))
            data[index] = 1;
        else
            data[index] = 0;
    }

    /// <summary>!(a && b) negated chain (must fold).</summary>
    public static void NegatedChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b)
    {
        if (!(a > 0 && b > 0))
            data[index] = 1;
        else
            data[index] = 0;
    }

    /// <summary>
    /// (a && (b || c)) || (d && e) deep mixed nesting — five conditional blocks
    /// that must fold to a single branch with a shared d&amp;e sub-DAG (§8.3).
    /// </summary>
    public static void DeepMixedKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b, int c, int d, int e)
    {
        if ((a > 0 && (b > 0 || c > 0)) || (d > 0 && e > 0))
            data[index] = 1;
        else
            data[index] = 0;
    }

    /// <summary>
    /// Three-exit region: if (a || b) X elif (c) Y else Z — must fold to the
    /// minimal two-branch ladder with don't-care-pruned step conditions (§8.1).
    /// </summary>
    public static void ThreeWayChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b, int c)
    {
        if (a > 0 || b > 0)
            data[index] = 10;
        else if (c > 0)
            data[index] = 20;
        else
            data[index] = 30;
    }

    /// <summary>
    /// Internal merge: a diamond producing a boolean that feeds a chain (§8.2).
    /// The inner phi becomes a genuine select network.
    /// </summary>
    public static void DiamondFeedChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b, int c, int d)
    {
        bool t = a > 0 ? b > 0 : c > 0;
        if (t && d > 0)
            data[index] = 1;
        else
            data[index] = 0;
    }

    /// <summary>
    /// Value merge with distinct per-edge phi values (select consolidation).
    /// </summary>
    public static void SelectMergeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b, int x, int y)
    {
        data[index] = a > 0 && b > 0 ? x : y;
    }

    /// <summary>
    /// An exit phi with three region sources merging to a nested select.
    /// </summary>
    public static void MultiExitPhiKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b, int x, int y, int z)
    {
        int result;
        if (a > 0)
            result = x;
        else if (b > 0)
            result = y;
        else
            result = z;
        data[index] = result;
    }

    /// <summary>
    /// A ten-term chain — folds under the default budgets (the budgets are
    /// safety valves, not a cost model; rejection paths are exercised by the
    /// small-budget detection unit tests).
    /// </summary>
    public static void DeepChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int v)
    {
        if ((v & 1) != 0 && (v & 2) != 0 && (v & 4) != 0 && (v & 8) != 0 &&
            (v & 16) != 0 && (v & 32) != 0 && (v & 64) != 0 && (v & 128) != 0 &&
            (v & 256) != 0 && (v & 512) != 0)
        {
            data[index] = 1;
        }
        else
        {
            data[index] = 0;
        }
    }

    /// <summary>
    /// Speculation-safety negative: the division must NOT be hoisted above its
    /// zero-guard — the region must not fold.
    /// </summary>
    public static void DivGuardKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b)
    {
        if (b != 0 && a / b > 2)
            data[index] = 1;
        else
            data[index] = 0;
    }

    static bool InRange(int x, int lo, int hi) =>
        x != lo && x != hi && (x & 3) != 0;

    static bool OutOfRange(int x, int lo, int hi) =>
        x == lo || x == hi || (x & 3) == 0;

    /// <summary>
    /// A chain of eight short-circuit predicates, each selecting one bit of
    /// the result: the if-converted predicates share their operations, and the
    /// IR must grow linearly with the chain length.
    /// </summary>
    public static void PredicateChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        data[index] =
            (InRange(x, 0, 3) ? 1 : 0) |
            (OutOfRange(x, 1, 4) ? 2 : 0) |
            (InRange(x, 2, 5) ? 4 : 0) |
            (OutOfRange(x, 3, 6) ? 8 : 0) |
            (InRange(x, 4, 7) ? 16 : 0) |
            (OutOfRange(x, 5, 8) ? 32 : 0) |
            (InRange(x, 6, 9) ? 64 : 0) |
            (OutOfRange(x, 7, 10) ? 128 : 0);
    }

    /// <summary>
    /// Speculation-safety negative in phi-argument form: the division is a phi
    /// argument of the guarded merge — the region must not fold.
    /// </summary>
    public static void DivGuardPhiKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b)
    {
        data[index] = b != 0 ? a / b : 0;
    }
}
