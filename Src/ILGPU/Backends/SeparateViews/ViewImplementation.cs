// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: ViewImplementation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Backends.SeparateViews
{
    /// <summary>
    /// Represents an array view that is not implemented directly
    /// and relies on separate driver support to map the actual device
    /// pointers to allocated memory buffers.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe readonly struct ViewImplementation
    {
        #region Static

        /// <summary>
        /// A handle to the <see cref="Create{T}(ArrayView{T})"/> method.
        /// </summary>
        private static readonly MethodInfo CreateMethod = typeof(ViewImplementation).
            GetMethod(nameof(Create))
            .ThrowIfNull();

        /// <summary>
        /// Returns a specialized create method.
        /// </summary>
        /// <param name="sourceType">The source array-view type.</param>
        /// <returns>The resolved creation method.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo GetCreateMethod(Type sourceType)
        {
            sourceType.IsArrayViewType(out Type? elementType);
            return CreateMethod.MakeGenericMethod(elementType.AsNotNull());
        }

        /// <summary>
        /// Gets the native-pointer method for the given element type.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <returns>The instantiated native method.</returns>
        public static MethodInfo GetNativePtrMethod(Type elementType) =>
            PointerViews.ViewImplementation.GetNativePtrMethod(elementType);

        /// <summary>
        /// Creates a new view implemented using the given array view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The source view.</param>
        /// <returns>The created view implementation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ViewImplementation Create<T>(ArrayView<T> source)
            where T : unmanaged =>
            new ViewImplementation(source.Index, source.Length);

        /// <summary>
        /// Returns the index field of a view implementation.
        /// </summary>
        /// <param name="implType">The view implementation type.</param>
        /// <returns>The resolved field.</returns>
        public static FieldInfo GetIndexField(Type implType) =>
            implType.GetField(nameof(Index)).AsNotNull();

        /// <summary>
        /// Returns the length field of a view implementation.
        /// </summary>
        /// <param name="implType">The view implementation type.</param>
        /// <returns>The resolved field.</returns>
        public static FieldInfo GetLengthField(Type implType) =>
            implType.GetField(nameof(Length)).AsNotNull();

        #endregion

        #region Instance

        /// <summary>
        /// The linear index into the view.
        /// </summary>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1051: DoNotDeclareVisibleInstanceFields",
            Justification = "Implementation type that simplifies code generation")]
        public readonly long Index;

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
        /// <param name="index">The index into the view.</param>
        /// <param name="length">The length information.</param>
        public ViewImplementation(long index, long length)
        {
            Index = index;
            Length = length;
        }

        #endregion
    }
}
