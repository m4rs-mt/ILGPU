// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ScanProvider.cs (obsolete)
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
        /// Creates a new specialized scan provider that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <returns>The created provider.</returns>
        [Obsolete("Use Accelerator.CreateScanProvider. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public ScanProvider CreateScanProvider()
        {
            return Accelerator.CreateScanProvider();
        }
    }
}
