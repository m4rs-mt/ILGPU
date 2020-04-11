// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ILABI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.PointerViews;
using ILGPU.IR.Types;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// Represents a platform-dependent IL ABI.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    sealed class ILABI : ViewImplementationABI
    {
        #region Instance

        /// <summary>
        /// Constructs a new IL ABI
        /// </summary>
        /// <param name="typeContext">The current type context.</param>
        /// <param name="targetPlatform">The target platform</param>
        public ILABI(
            IRTypeContext typeContext,
            TargetPlatform targetPlatform)
            : base(typeContext, targetPlatform)
        { }

        #endregion
    }
}
