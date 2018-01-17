// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Interop.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler.Intrinsic;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU
{
    /// <summary>
    /// Contains general interop functions.
    /// </summary>
    public static class Interop
    {
        /// <summary>
        /// Returns a reference that references given address.
        /// </summary>
        /// <param name="value">A pointer to a variable of type <typeparamref name="T"/>.</param>
        /// <returns>A reference that references given address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T GetRef<T>(IntPtr value)
        {
            return ref Unsafe.AsRef<T>(value.ToPointer());
        }

        /// <summary>
        /// Returns a pointer that points to the given managed reference.
        /// </summary>
        /// <param name="variableRef">The variable reference.</param>
        /// <returns>A pointer that points to the given managed reference.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr GetAddress<T>(ref T variableRef)
            where T : struct
        {
            return new IntPtr(Unsafe.AsPointer(ref variableRef));
        }

        /// <summary>
        /// Loads a structure of type T from the given memory address that is adjusted
        /// with the help of elementSize * elementIndex.
        /// </summary>
        /// <typeparam name="T">The structure type.</typeparam>
        /// <param name="ptr">The source address.</param>
        /// <param name="elementSize">The size in bytes of a single element.</param>
        /// <param name="elementIndex">The index of the target element.</param>
        /// <returns>The loaded structure of type T.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "ptr")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T PtrToStructure<T>(IntPtr ptr, int elementSize, int elementIndex)
            where T : struct
        {
            ptr = LoadEffectiveAddress(ptr, elementSize, elementIndex);
            return PtrToStructure<T>(ptr);
        }

        /// <summary>
        /// Loads a structure of type T from the given memory address.
        /// </summary>
        /// <typeparam name="T">The structure type.</typeparam>
        /// <param name="ptr">The source address.</param>
        /// <returns>The loaded structure of type T.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "ptr")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T PtrToStructure<T>(IntPtr ptr)
            where T : struct
        {
            return Unsafe.Read<T>(ptr.ToPointer());
        }

        /// <summary>
        /// Stores a structure of type T into the given memory address that is adjusted
        /// with the help of elementSize * elementIndex.
        /// </summary>
        /// <typeparam name="T">The structure type.</typeparam>
        /// <param name="source">The source structure.</param>
        /// <param name="ptr">The target address.</param>
        /// <param name="elementSize">The size in bytes of a single element.</param>
        /// <param name="elementIndex">The index of the target element.</param>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "ptr")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StructureToPtr<T>(T source, IntPtr ptr, int elementSize, int elementIndex)
            where T : struct
        {
            ptr = LoadEffectiveAddress(ptr, elementSize, elementIndex);
            StructureToPtr(source, ptr);
        }

        /// <summary>
        /// Stores a structure of type T into the given memory address.
        /// </summary>
        /// <typeparam name="T">The structure type.</typeparam>
        /// <param name="source">The source structure.</param>
        /// <param name="ptr">The target address.</param>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "ptr")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void StructureToPtr<T>(T source, IntPtr ptr)
            where T : struct
        {
            Unsafe.Write(ptr.ToPointer(), source);
        }

        /// <summary>
        /// Destroys a structure of type T at the given memory address.
        /// </summary>
        /// <typeparam name="T">The structure type.</typeparam>
        /// <param name="ptr">The target address.</param>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "ptr")]
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "Matches signature of Marshal.DestroyStructure")]
        [InteropIntrinsic(InteropIntrinsicKind.DestroyStructure)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void DestroyStructure<T>(IntPtr ptr)
            where T : struct
        {
            StructureToPtr(default(T), ptr);
        }

        /// <summary>
        /// Computes the size of the given type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "Matches signature of Marshal.SizeOf")]
        [InteropIntrinsic(InteropIntrinsicKind.SizeOf)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>()
        {
            return Unsafe.SizeOf<T>();
        }

        /// <summary>
        /// Computes the size of the given type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(T structure)
        {
            return SizeOf<T>();
        }

        /// <summary>
        /// Computes the unsigned offset of the given field in bytes.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="fieldName">The name of the target field.</param>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "The type is required for the computation of the field offset")]
        [InteropIntrinsic(InteropIntrinsicKind.OffsetOf)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }

        /// <summary>
        /// Loads the effective address by computing <paramref name="basePointer"/> +
        /// <paramref name="globalOffset"/>.
        /// </summary>
        /// <param name="basePointer">The base pointer.</param>
        /// <param name="globalOffset">The global offset in bytes.</param>
        /// <returns>The effective address.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "pointer")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr LoadEffectiveAddress(IntPtr basePointer, int globalOffset)
        {
            var ptr = (byte*)basePointer.ToPointer() + globalOffset;
            return new IntPtr(ptr);
        }

        /// <summary>
        /// Loads the effective address by computing <paramref name="basePointer"/> +
        /// <paramref name="elementSize"/> * <paramref name="elementIndex"/>
        /// </summary>
        /// <param name="basePointer">The base pointer.</param>
        /// <param name="elementSize">The size of a single element in bytes.</param>
        /// <param name="elementIndex">The index of the target element.</param>
        /// <returns>The effective address.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "pointer")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr LoadEffectiveAddress(IntPtr basePointer, int elementSize, int elementIndex)
        {
            return LoadEffectiveAddress(basePointer, elementSize * elementIndex);
        }
    }
}
