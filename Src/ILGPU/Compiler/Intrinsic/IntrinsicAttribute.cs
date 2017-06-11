// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: IntrinsicAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Compiler.Intrinsic
{
    enum IntrinsicType
    {
        Atomic,
        MemoryFence,
        Grid,
        Group,
        Warp,
        Math,
        Interop,
    }

    /// <summary>
    /// Marks methods that are builtin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    abstract class IntrinsicAttribute : Attribute
    {
        /// <summary>
        /// Returns the type of this intrinsic attribute.
        /// </summary>
        public abstract IntrinsicType Type { get; }
    }
}
