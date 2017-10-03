// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: LightningObject.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using System;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents the base class for all objects that need to
    /// reference an accelerator.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class LightningObject : DisposeBase
    {
        #region Instance

        /// <summary>
        /// Constructs a new context object.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        protected LightningObject(Accelerator accelerator)
        {
            Accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public Accelerator Accelerator { get; }

        #endregion
    }
}
