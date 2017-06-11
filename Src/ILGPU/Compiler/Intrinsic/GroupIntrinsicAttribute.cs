// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: GroupIntrinsicAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Compiler.Intrinsic
{
    enum GroupIntrinsicKind
    {
        Barrier,
        BarrierPopCount,
        BarrierAnd,
        BarrierOr,
    }

    /// <summary>
    /// Marks group methods that are builtin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class GroupIntrinsicAttribute : IntrinsicAttribute
    {
        public GroupIntrinsicAttribute(GroupIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.Group;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public GroupIntrinsicKind IntrinsicKind { get; }
    }
}
