// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: CudaRadixSortProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace ILGPU.Lightning
{
    partial class RadixSortExtensions
    {
        partial struct RadixSortExtension
        {
            public RadixSortProviderImplementation CreateCudaExtension(CudaAccelerator accelerator)
            {
                return new Cuda.CudaRadixSortProviderImplementation(accelerator);
            }
        }

        partial struct RadixSortPairsExtension
        {
            public RadixSortPairsProviderImplementation CreateCudaExtension(CudaAccelerator accelerator)
            {
                return new Cuda.CudaRadixSortPairsProviderImplementation(accelerator);
            }
        }
    }
}

namespace ILGPU.Lightning.Cuda
{
    sealed partial class CudaRadixSortProviderImplementation : RadixSortProviderImplementation
    {
        #region Instance

        internal CudaRadixSortProviderImplementation(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion
    }

    sealed partial class CudaRadixSortPairsProviderImplementation : RadixSortPairsProviderImplementation
    {
        #region Instance

        internal CudaRadixSortPairsProviderImplementation(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion
    }
}
