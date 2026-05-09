// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ViewImplementation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime;

/// <summary>
/// Represents an array view as a raw pointer + length pair for kernel marshaling.
/// Used by ILGPUC-generated CompiledKernel classes to convert between the user-facing
/// <see cref="ArrayView{T}"/> type and the low-level representation expected by
/// compiled kernel entry points.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
[StructLayout(LayoutKind.Sequential)]
public unsafe readonly struct ViewImplementation<T>
    where T : unmanaged
{
    /// <summary>
    /// The base pointer.
    /// </summary>
    [SuppressMessage(
        "Microsoft.Design",
        "CA1051: DoNotDeclareVisibleInstanceFields",
        Justification = "Implementation type that simplifies code generation")]
    public readonly void* Ptr;

    /// <summary>
    /// The length.
    /// </summary>
    [SuppressMessage(
        "Microsoft.Design",
        "CA1051: DoNotDeclareVisibleInstanceFields",
        Justification = "Implementation type that simplifies code generation")]
    public readonly long Length;

    /// <summary>
    /// Constructs a new array view implementation.
    /// </summary>
    /// <param name="ptr">The base pointer.</param>
    /// <param name="length">The length information.</param>
    public ViewImplementation(void* ptr, long length)
    {
        Ptr = ptr;
        Length = length;
    }

    /// <summary>
    /// Constructs a new array view implementation from an abstract source view.
    /// </summary>
    /// <param name="source">The abstract source view.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ViewImplementation(ArrayView<T> source)
        : this(
              source.IsValid
              ? Unsafe.AsPointer(ref source.LoadEffectiveAddress())
              : null,
              source.Length)
    { }
}
