// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: CPUScanProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace ILGPU.Lightning
{
    partial class ScanExtensions
    {
        partial struct ScanExtension
        {
            public ScanProviderImplementation CreateCPUExtension(CPUAccelerator accelerator)
            {
                return new CPU.CPUScanProviderImplementation(accelerator);
            }
        }
    }
}

namespace ILGPU.Lightning.CPU
{
    sealed partial class CPUScanProviderImplementation : ScanProviderImplementation
    {
        #region Instance

        internal CPUScanProviderImplementation(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion
    }
}
