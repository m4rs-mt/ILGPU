// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: NotInsideKernelAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Marks methods and constructors that are not supported within kernels.
    /// </summary>
    /// <remarks>
    /// This attribute is not required but helps us to generate better error messages.
    /// </remarks>
    [AttributeUsage(
        AttributeTargets.Method | AttributeTargets.Constructor,
        AllowMultiple = false)]
    public sealed class NotInsideKernelAttribute : Attribute
    {
        /// <summary>
        /// The static type reference.
        /// </summary>
        internal static readonly Type AttributeType = typeof(NotInsideKernelAttribute);

        /// <summary>
        /// Returns true if the method is annotated with the attribute
        /// <see cref="NotInsideKernelAttribute"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDefined(MethodBase method) =>
            method.IsDefined(AttributeType);

        // ...
    }
}
