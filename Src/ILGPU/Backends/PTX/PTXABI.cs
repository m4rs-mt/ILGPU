// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXABI.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.PointerViews;
using ILGPU.IR.Types;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents a platform-dependent PTX ABI.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    sealed class PTXABI : ViewImplementationABI
    {
        #region Instance

        /// <summary>
        /// Constructs a new PTX ABI
        /// </summary>
        /// <param name="typeContext">The current type context.</param>
        /// <param name="targetPlatform">The target platform</param>
        public PTXABI(
            IRTypeContext typeContext,
            TargetPlatform targetPlatform)
            : base(typeContext, targetPlatform)
        {
            // Bools are mapped to 32bit int registers in PTX
            DefineBasicTypeInformation(BasicValueType.Int1, 4);
        }

        #endregion
    }
}
