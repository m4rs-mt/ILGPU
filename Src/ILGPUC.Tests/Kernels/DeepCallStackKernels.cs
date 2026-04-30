// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DeepCallStackKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Round-3 derisking: a deep call chain (8 frames) ending in a remappable
/// BCL intrinsic. Each layer performs a different small bit/arithmetic op
/// so the chain doesn't collapse to a single inlined expression at low
/// opt levels, and so the operand walk has to traverse every layer to
/// reach <see cref="Math.Abs(int)"/> at the bottom.
///
/// The kernel exercises:
///   1. Eager BFS through walkable code (rule a — entry assembly is
///      ILGPUC.Tests, all helpers live here too) — confirms the BFS
///      reaches a leaf method whose body calls a remapped BCL method.
///   2. <see cref="ILGPUC.Frontend.Intrinsic.Intrinsics.TryImplement"/>
///      remaps <c>Math.Abs(int)</c> → <c>XMath.Abs(int)</c> when
///      <see cref="ILGPUC.Frontend.ILFrontend.ProcessMethod"/> visits
///      <c>Math.Abs</c> as an operand of <see cref="Layer8"/>.
///   3. The codegen-time fallback remap inside
///      <see cref="ILGPUC.Frontend.Intrinsic.Intrinsics.TryGenerateCode"/>
///      provides a safety net even for chains where the eager BFS skips
///      a method (round-3 walkability gate). For walkable chains, Path A
///      handles things; if any layer were ever moved to a non-walkable
///      assembly, Path B would still emit the correct intrinsic.
/// </summary>
static class DeepCallStackKernels
{
    // Helpers are `internal` so KernelRegistry's auto-discovery
    // (Public | Static methods of any *Kernels class) does NOT register
    // them as standalone kernels — only DeepChainAbsKernel below is a
    // kernel. Reflection-based disassembly in ILFrontend works on
    // internal methods just fine.
    internal static int Layer8(int x) => Math.Abs(x);
    internal static int Layer7(int x) => Layer8(x) ^ 0;
    internal static int Layer6(int x) => Layer7(x) + 0;
    internal static int Layer5(int x) => Layer6(x) - 0;
    internal static int Layer4(int x) => Layer5(x) | 0;
    internal static int Layer3(int x) => Layer4(x) & ~0;
    internal static int Layer2(int x) => Layer3(x) << 0;
    internal static int Layer1(int x) => Layer2(x) >> 0;

    /// <summary>
    /// Kernel that drives the 8-deep helper chain. Each kernel invocation
    /// calls into <see cref="Layer1"/>, which transitively calls
    /// <see cref="Layer8"/>, which calls <see cref="Math.Abs(int)"/> — the
    /// intrinsic the compiler must remap to <c>XMath.Abs</c> /
    /// <c>@llvm.abs.i32</c>.
    /// </summary>
    public static void DeepChainAbsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        result[index] = Layer1(input[index]);
    }
}
