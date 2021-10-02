// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: ViewImplementation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Backends.PointerViews
{
    /// <summary>
    /// Represents an array view that is implemented with the help of
    /// native pointers.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    [CLSCompliant(false)]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe readonly struct ViewImplementation<T>
        where T : unmanaged
    {
        #region Instance

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
        /// Constructs a new array view implementation.
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

        #endregion

        #region Properties

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public ref T this[Index1D index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LoadElementAddress(index);
        }

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public ref T this[LongIndex1D index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LoadElementAddress(index);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T LoadElementAddress(Index1D index) =>
            ref LoadElementAddress((LongIndex1D)index);

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T LoadElementAddress(LongIndex1D index) =>
            ref Unsafe.Add(ref Unsafe.AsRef<T>(Ptr), new IntPtr(index));

        #endregion
    }
}
