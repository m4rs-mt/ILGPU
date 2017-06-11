// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: LightningContextObject.cs
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
    /// reference a lightning context.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class LightningContextObject : DisposeBase
    {
        #region Instance

        /// <summary>
        /// Constructs a new context object.
        /// </summary>
        /// <param name="lightningContext">The associated lightning context.</param>
        protected LightningContextObject(LightningContext lightningContext)
        {
            LightningContext = lightningContext ?? throw new ArgumentNullException(nameof(lightningContext));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public Accelerator Accelerator => LightningContext.Accelerator;

        /// <summary>
        /// Returns the associated lightning context.
        /// </summary>
        public LightningContext LightningContext { get; }

        #endregion
    }
}
