// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: CPURadixSortProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime.CPU;

namespace ILGPU.Lightning
{
    partial class LightningContext
    {
        partial struct RadixSortExtension
        {
            public RadixSortProviderImplementation CreateCPUExtension(CPUAccelerator accelerator)
            {
                return new CPU.CPURadixSortProviderImplementation(LightningContext);
            }
        }

        partial struct RadixSortPairsExtension
        {
            public RadixSortPairsProviderImplementation CreateCPUExtension(CPUAccelerator accelerator)
            {
                return new CPU.CPURadixSortPairsProviderImplementation(LightningContext);
            }
        }
    }
}

namespace ILGPU.Lightning.CPU
{
    sealed partial class CPURadixSortProviderImplementation : RadixSortProviderImplementation
    {
        #region Instance

        internal CPURadixSortProviderImplementation(LightningContext lightningContext)
            : base(lightningContext)
        { }

        #endregion
    }

    sealed partial class CPURadixSortPairsProviderImplementation : RadixSortPairsProviderImplementation
    {
        #region Instance

        internal CPURadixSortPairsProviderImplementation(LightningContext lightningContext)
            : base(lightningContext)
        { }

        #endregion
    }
}
