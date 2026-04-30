// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PerfRegressionKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Trivial kernels held stable for compile-time perf regression tracking.
/// Mirrors <c>ILGPUC.CompileBench.Kernels.VectorMul</c> so the regression
/// test asserts work-done counters against the same canonical workload the
/// CompileBench tool reports on. Kept in a separate file (rather than
/// reusing other Kernels classes) so any churn in those files cannot
/// silently move the locked baselines.
/// </summary>
static class PerfRegressionKernels
{
    /// <summary>
    /// Canonical perf-tracking kernel: <c>a[i] = b[i] * c[i]</c>.
    /// </summary>
    public static void VectorMul(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> a,
        ArrayView1D<float, Stride1D.Dense> b,
        ArrayView1D<float, Stride1D.Dense> c)
    {
        a[index] = b[index] * c[index];
    }

    /// <summary>
    /// A second trivial kernel used by the cache-reuse test. Touches the
    /// same view machinery as <see cref="VectorMul"/> so when the two are
    /// compiled back-to-back through one shared <c>ILFrontendCache</c> the
    /// second pays no fresh disassembly cost for shared bodies.
    /// </summary>
    public static void VectorAdd(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> a,
        ArrayView1D<float, Stride1D.Dense> b,
        ArrayView1D<float, Stride1D.Dense> c)
    {
        a[index] = b[index] + c[index];
    }
}
