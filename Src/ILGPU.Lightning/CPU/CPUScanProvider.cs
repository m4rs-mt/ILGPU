// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: CPUScanProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime.CPU;

namespace ILGPU.Lightning
{
    partial class LightningContext
    {
        partial struct ScanExtension
        {
            public ScanProviderImplementation CreateCPUExtension(CPUAccelerator accelerator)
            {
                return new CPU.CPUScanProviderImplementation(LightningContext);
            }
        }
    }
}

namespace ILGPU.Lightning.CPU
{
    sealed partial class CPUScanProviderImplementation : ScanProviderImplementation
    {
        #region Instance

        internal CPUScanProviderImplementation(LightningContext lightningContext)
            : base(lightningContext)
        { }

        #endregion
    }
}
