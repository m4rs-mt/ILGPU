// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: InitializeExtensions.cs (obsolete)
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using System;

namespace ILGPU.Lightning
{
    partial class LightningContext
    {
        /// <summary>
        /// Creates an initializer that is defined by the given element type.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <returns>The loaded transformer.</returns>
        [Obsolete("Use Accelerator.CreateSequencer. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Initializer<T> CreateInitializer<T>()
            where T : struct
        {
            return Accelerator.CreateInitializer<T>();
        }

        /// <summary>
        /// Performs an initialization on the given view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The element view.</param>
        /// <param name="value">The target value.</param>
        [Obsolete("Use Accelerator.Initialize. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Initialize<T>(ArrayView<T> view, T value)
            where T : struct
        {
            Accelerator.Initialize(view, value);
        }

        /// <summary>
        /// Performs an initialization on the given view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The element view.</param>
        /// <param name="value">The target value.</param>
        [Obsolete("Use Accelerator.Initialize. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Initialize<T>(AcceleratorStream stream, ArrayView<T> view, T value)
            where T : struct
        {
            Accelerator.Initialize(stream, view, value);
        }
    }
}
