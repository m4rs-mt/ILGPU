// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: ViewImplementation.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

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
        where T : struct
    {
        #region Instance

        /// <summary>
        /// The base pointer.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1051: DoNotDeclareVisibleInstanceFields",
            Justification = "Implementation type that simplifies code generation")]
        [SuppressMessage("Microsoft.Security", "CA2104: DoNotDeclareReadOnlyMutableReferenceTypes",
            Justification = "This structure is used for marshalling purposes only. The reference will not be accessed using this structure.")]
        public readonly void* Ptr;

        /// <summary>
        /// The length.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1051: DoNotDeclareVisibleInstanceFields",
            Justification = "Implementation type that simplifies code generation")]
        public readonly int Length;

        /// <summary>
        /// Constructs a new array view implementation.
        /// </summary>
        /// <param name="ptr">The base pointer.</param>
        /// <param name="length">The length information.</param>
        public ViewImplementation(void* ptr, int length)
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
            : this(source.IsValid ? source.LoadEffectiveAddress() : null, source.Length)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public ref T this[Index index]
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
        public ref T LoadElementAddress(Index index) =>
            ref Unsafe.Add(ref Unsafe.AsRef<T>(Ptr), index);

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="offset">The starting offset.</param>
        /// <param name="length">The extent of the new subview.</param>
        /// <returns>The new subview.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ViewImplementation<T> GetSubView(Index offset, Index length) =>
            new ViewImplementation<T>(
                Unsafe.AsPointer(ref this[offset]),
                length);

        /// <summary>
        /// Casts the view into another view with a different element type.
        /// </summary>
        /// <typeparam name="TOther">The other element type.</typeparam>
        /// <returns>The casted view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ViewImplementation<TOther> Cast<TOther>()
            where TOther : struct =>
            new ViewImplementation<TOther>(Ptr, Length);

        #endregion
    }
}
