// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: MemoryFenceIntrinsicAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Compiler.Intrinsic
{
    enum MemoryFenceIntrinsicKind
    {
        GroupLevel,
        DeviceLevel,
        SystemLevel,
    }

    /// <summary>
    /// Marks memory-fence methods that are builtin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class MemoryFenceIntrinsicAttribute : IntrinsicAttribute
    {
        public MemoryFenceIntrinsicAttribute(MemoryFenceIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.MemoryFence;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public MemoryFenceIntrinsicKind IntrinsicKind { get; }
    }
}
