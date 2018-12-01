// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: LightningObject.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents the base class for all objects that need to
    /// reference an accelerator.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class LightningObject : AcceleratorObject
    {
        #region Instance

        /// <summary>
        /// Constructs a new context object.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        protected LightningObject(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion
    }
}
