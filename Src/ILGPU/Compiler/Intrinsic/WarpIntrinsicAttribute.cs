// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: WarpIntrinsicAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Compiler.Intrinsic
{
    enum WarpIntrinsicKind
    {
        WarpSize,
        LaneIdx,

        ShuffleI32,
        ShuffleF32,

        ShuffleDownI32,
        ShuffleDownF32,

        ShuffleUpI32,
        ShuffleUpF32,

        ShuffleXorI32,
        ShuffleXorF32,
    }

    /// <summary>
    /// Marks warp methods that are builtin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class WarpIntrinsicAttribute : IntrinsicAttribute
    {
        public WarpIntrinsicAttribute(WarpIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.Warp;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public WarpIntrinsicKind IntrinsicKind { get; }
    }
}
