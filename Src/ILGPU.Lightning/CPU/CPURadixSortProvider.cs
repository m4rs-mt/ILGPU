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

using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace ILGPU.Lightning
{
    partial class RadixSortExtensions
    {
        partial struct RadixSortExtension
        {
            public RadixSortProviderImplementation CreateCPUExtension(CPUAccelerator accelerator)
            {
                return new CPU.CPURadixSortProviderImplementation(accelerator);
            }
        }

        partial struct RadixSortPairsExtension
        {
            public RadixSortPairsProviderImplementation CreateCPUExtension(CPUAccelerator accelerator)
            {
                return new CPU.CPURadixSortPairsProviderImplementation(accelerator);
            }
        }
    }
}

namespace ILGPU.Lightning.CPU
{
    sealed partial class CPURadixSortProviderImplementation : RadixSortProviderImplementation
    {
        #region Instance

        internal CPURadixSortProviderImplementation(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion
    }

    sealed partial class CPURadixSortPairsProviderImplementation : RadixSortPairsProviderImplementation
    {
        #region Instance

        internal CPURadixSortPairsProviderImplementation(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion
    }
}
