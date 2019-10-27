// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLABI.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.PointerViews;
using ILGPU.IR.Types;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Represents a platform-dependent OpenCL ABI.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    sealed class CLABI : ViewImplementationABI
    {
        #region Instance

        /// <summary>
        /// Constructs a new OpenCL ABI
        /// </summary>
        /// <param name="typeContext">The current type context.</param>
        /// <param name="targetPlatform">The target platform</param>
        public CLABI(
            IRTypeContext typeContext,
            TargetPlatform targetPlatform)
            : base(typeContext, targetPlatform)
        {
            // Bools are mapped to bytes in OpenCL
            DefineBasicTypeInformation(BasicValueType.Int1, 1);
        }

        #endregion
    }
}
