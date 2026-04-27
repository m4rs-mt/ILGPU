// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AdvancedViewKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// User-defined struct used by <see cref="AdvancedViewKernels"/>. Exercises
/// <see cref="Interop.OffsetOf{T}(string)"/> as a static readonly field —
/// a pattern that requires the launcher emitter to marshal a view of a
/// user struct correctly (Family A.1 regression).
/// </summary>
// Fields are written via a reinterpreted byte view (Cast<byte>().SubView(...)),
// so the C# compiler can't see the assignment.
#pragma warning disable CS0649
struct ComposedView
{
    public static readonly int ElementCounterOffset =
        Interop.OffsetOf<ComposedView>(nameof(ElementCounter));

    public short SomeElement;
    public byte SomeOtherElement;
    public int ElementCounter;
}
#pragma warning restore CS0649

/// <summary>
/// Kernels that exercise launcher view-of-struct marshalling and runtime
/// view reinterpretation. Mirrors <c>Samples/AdvancedViews</c> and pins the
/// FlatStructLauncherEmitter regression fixed on <c>temp5</c>.
/// </summary>
static class AdvancedViewKernels
{
    /// <summary>
    /// Walks two views — an int array and a single-element user-struct
    /// view — and reinterprets the struct view as bytes, slices to the
    /// counter field's offset, casts back to int, and atomically increments.
    /// Stresses the launcher's struct-view marshalling and the C-like
    /// emitter's pointer-cast element-type handling.
    /// </summary>
    public static void ViewOfStructCounterKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> elements,
        ArrayView<ComposedView> view,
        int comparisonValue)
    {
        var element = elements[index];
        if (element == comparisonValue)
        {
            var byteView = view.Cast<byte>();
            int byteOffset = ComposedView.ElementCounterOffset;
            var intView = byteView.SubView(byteOffset).Cast<int>();
            Atomic.Add(ref intView[0], 1);
        }
    }
}
