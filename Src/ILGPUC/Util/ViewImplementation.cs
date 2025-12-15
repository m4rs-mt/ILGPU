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

using ILGPU;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPUC.Util;

/// <summary>
/// Represents an array view that is implemented with the help of
/// native pointers.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
[StructLayout(LayoutKind.Sequential)]
unsafe readonly struct ViewImplementation<T>
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

/// <summary>
/// General extensions for pointer-based array view implementations.
/// </summary>
static class ViewImplementation
{
    /// <summary>
    /// The generic implementation type.
    /// </summary>
    public static readonly Type ImplementationType = typeof(ViewImplementation<>);

    /// <summary>
    /// Returns a specialized implementation type.
    /// </summary>
    /// <param name="elementType">The view element type.</param>
    /// <returns>The implement type.</returns>
    public static Type GetImplementationType(Type elementType) =>
        ImplementationType.MakeGenericType(elementType);

    /// <summary>
    /// Append all implementation-specific element types.
    /// </summary>
    /// <typeparam name="TCollection">The target collection type.</typeparam>
    /// <param name="collection">The target element collection.</param>
    public static void AppendImplementationTypes<TCollection>(TCollection collection)
        where TCollection : ICollection<Type>
    {
        collection.Add(typeof(void*));
        collection.Add(typeof(long));
    }

    /// <summary>
    /// Returns a specialized view constructor.
    /// </summary>
    /// <param name="implType">The view implementation type.</param>
    /// <returns>The resolved view constructor.</returns>
    public static ConstructorInfo GetViewConstructor(Type implType) =>
        implType.GetConstructor(new Type[]
        {
                typeof(ArrayView<>).MakeGenericType(
                    implType.GetGenericArguments()[0])
        })
        .ThrowIfNull();

    /// <summary>
    /// Returns the pointer field of a view implementation.
    /// </summary>
    /// <param name="implType">The view implementation type.</param>
    /// <returns>The resolved field.</returns>
    public static FieldInfo GetPtrField(Type implType) =>
        implType.GetField(nameof(ViewImplementation<int>.Ptr)).ThrowIfNull();

    /// <summary>
    /// Returns the length field of a view implementation.
    /// </summary>
    /// <param name="implType">The view implementation type.</param>
    /// <returns>The resolved field.</returns>
    public static FieldInfo GetLengthField(Type implType) =>
        implType.GetField(nameof(ViewImplementation<int>.Length)).ThrowIfNull();

    /// <summary>
    /// The method handle of the <see cref="GetNativePtrMethod(Type)"/> method.
    /// </summary>
    private static readonly MethodInfo GetNativePtrMethodInfo =
        typeof(ViewImplementation).GetMethod(
            nameof(GetNativePtr),
            BindingFlags.NonPublic | BindingFlags.Static)
        .ThrowIfNull();

    /// <summary>
    /// Gets the associated native pointer that is stored inside the given view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="view">The view type.</param>
    /// <returns>The underlying native pointer.</returns>
    private static IntPtr GetNativePtr<T>(in ArrayView<T> view)
        where T : unmanaged =>
        view.Buffer?.NativePtr ?? IntPtr.Zero;

    /// <summary>
    /// Gets the native-pointer method for the given element type.
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <returns>The instantiated native method.</returns>
    public static MethodInfo GetNativePtrMethod(Type elementType) =>
        GetNativePtrMethodInfo.MakeGenericMethod(elementType);
}