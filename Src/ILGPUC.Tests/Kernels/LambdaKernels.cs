// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LambdaKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for lambda/closure tests.
/// </summary>
static class LambdaKernels
{
    public static void SimpleCaptureKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int addend)
    {
        int x = addend;
        Func<int, int> f = y => y + x;
        data[index] = f(index.X);
    }

    public static void MultiCaptureKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int a, int b)
    {
        Func<int, int> f = x => x * a + b;
        data[index] = f(index.X);
    }

    public static void LambdaConditionalKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int threshold)
    {
        int t = threshold;
        Func<int, int> clamp = x => x > t ? t : x;
        data[index] = clamp(index.X);
    }

    public static void LambdaAppliedTwiceKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int offset)
    {
        int off = offset;
        Func<int, int> shift = x => x + off;
        data[index] = shift(index.X) + shift(index.X + 1);
    }

    public static void AccumulateClosureKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        Func<int, int> add = x => { sum += x; return sum; };
        add(index);
        add(index + 1);
        data[index] = sum;
    }

    public static void ClosureReadAfterWriteKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int state = index;
        Func<int> read = () => state;
        Action<int> write = x => { state = x * 2; };
        write(read());
        data[index] = read();
    }

    /// <summary>
    /// Non-capturing (static) lambda: (a, b) => a + b.
    /// C# compiler caches this in a singleton <>c.<>9 pattern.
    /// </summary>
    public static void StaticLambdaAddKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        Func<int, int, int> add = (a, b) => a + b;
        data[index] = add(index.X, 1);
    }

    /// <summary>
    /// Non-capturing (static) lambda invoked directly without local variable.
    /// </summary>
    public static void StaticLambdaInlineKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        Func<int, int> doubleIt = x => x * 2;
        data[index] = doubleIt(index.X);
    }

    /// <summary>
    /// Two different non-capturing lambdas stored to locals in one method.
    /// The ternary argument forces each delegate handle across a block
    /// boundary on the evaluation stack, so the Invoke sites resolve through
    /// the cross-block phi-tracing path in TryResolveDelegateMethod.
    /// Regression test: each call site must devirtualize to its own lambda
    /// body (the old fallback returned the first table entry for both).
    /// </summary>
    public static void TwoLambdasCrossBlockKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int flag)
    {
        Func<int, int> inc = x => x + 1;
        Func<int, int> dbl = x => x * 2;
        data[index] =
            inc(flag > 0 ? index.X : 0) +
            dbl(flag > 0 ? index.X : 1);
    }

    /// <summary>
    /// Two different capturing lambdas stored to locals in one method, with
    /// cross-block delegate handles (see TwoLambdasCrossBlockKernel). Covers
    /// the closure-object handle path in addition to the static-lambda path.
    /// </summary>
    public static void TwoClosuresCrossBlockKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int a, int b)
    {
        Func<int, int> addA = x => x + a;
        Func<int, int> mulB = x => x * b;
        data[index] =
            addA(index.X > 0 ? index.X : a) +
            mulB(index.X > 0 ? index.X : b);
    }

    /// <summary>
    /// A lambda capturing locals of two nested scopes, which the C# compiler
    /// stores in two closure classes: the inner one refers to the outer one
    /// through a field of a class type that occupies a single pointer field.
    /// </summary>
    public static void NestedScopeCaptureKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int offset = index * 10;
        int scale = 2;
        if (index >= 0)
        {
            int bias = 3;
            Func<int, int> f = x => x * scale + offset + bias;
            data[index] = f(index);
        }
    }
}
