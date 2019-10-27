// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLCodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Generates OpenCL source code out of IR values.
    /// </summary>
    /// <remarks>The code needs to be prepared for this code generator.</remarks>
    public partial class CLCodeGenerator : CLRegisterAllocator
    {
        /// <summary>
        /// Constructs a new code generator.
        /// </summary>
        /// <param name="abi">The current ABI.</param>
        public CLCodeGenerator(ABI abi)
            : base(abi)
        {
        }
    }
}
