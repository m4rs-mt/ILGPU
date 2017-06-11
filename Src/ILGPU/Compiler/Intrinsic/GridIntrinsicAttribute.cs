// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: GridIntrinsicAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Compiler.Intrinsic
{
    enum GridIntrinsicKind
    {
        GetGridDimension,
        GetGroupDimension,
    }

    /// <summary>
    /// Marks grid methods that are builtin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class GridIntrinsicAttribute : IntrinsicAttribute
    {
        public GridIntrinsicAttribute(GridIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.Grid;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public GridIntrinsicKind IntrinsicKind { get; }
    }
}
