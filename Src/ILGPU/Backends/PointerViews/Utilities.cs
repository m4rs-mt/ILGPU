// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Utilities.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Reflection;

namespace ILGPU.Backends.PointerViews
{
    /// <summary>
    /// General extensions for pointer-based array view implementations.
    /// </summary>
    public static class ViewImplementation
    {
        /// <summary>
        /// The generic implementation type.
        /// </summary>
        public static readonly Type ImplementationType = typeof(ViewImplementation<>);

        /// <summary>
        /// Returns a specialized implementation type.
        /// </summary>
        /// <param name="elementType">The view element type.</param>
        /// <returns></returns>
        public static Type GetImplementationType(Type elementType) =>
            ImplementationType.MakeGenericType(elementType);

        /// <summary>
        /// Returns a specialized pointer constructor.
        /// </summary>
        /// <param name="implType">The view implementation type.</param>
        /// <returns>The resolved pointer constructor.</returns>
        public static ConstructorInfo GetPointerConstructor(Type implType)
        {
            return implType.GetConstructor(new Type[]
            {
                typeof(void).MakePointerType(),
                typeof(Index),
            });
        }

        /// <summary>
        /// Returns a specialized view constructor.
        /// </summary>
        /// <param name="implType">The view implementation type.</param>
        /// <returns>The resolved view constructor.</returns>
        public static ConstructorInfo GetViewConstructor(Type implType)
        {
            return implType.GetConstructor(new Type[]
            {
                typeof(ArrayView<>).MakeGenericType(implType.GetGenericArguments()[0])
            });
        }

        /// <summary>
        /// Returns the pointer field of a view implementation.
        /// </summary>
        /// <param name="implType">The view implementation type.</param>
        /// <returns>The resolved field.</returns>
        public static FieldInfo GetPtrField(Type implType) =>
            implType.GetField(nameof(ViewImplementation<int>.Ptr));

        /// <summary>
        /// Returns the length field of a view implementation.
        /// </summary>
        /// <param name="implType">The view implementation type.</param>
        /// <returns>The resolved field.</returns>
        public static FieldInfo GetLengthField(Type implType) =>
            implType.GetField(nameof(ViewImplementation<int>.Length));
    }
}
