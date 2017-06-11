// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: CudaRadixSortProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime.Cuda;

namespace ILGPU.Lightning
{
    partial class LightningContext
    {
        partial struct RadixSortExtension
        {
            public RadixSortProviderImplementation CreateCudaExtension(CudaAccelerator accelerator)
            {
                return new Cuda.CudaRadixSortProviderImplementation(LightningContext);
            }
        }

        partial struct RadixSortPairsExtension
        {
            public RadixSortPairsProviderImplementation CreateCudaExtension(CudaAccelerator accelerator)
            {
                return new Cuda.CudaRadixSortPairsProviderImplementation(LightningContext);
            }
        }
    }
}

namespace ILGPU.Lightning.Cuda
{
    sealed partial class CudaRadixSortProviderImplementation : RadixSortProviderImplementation
    {
        #region Instance

        internal CudaRadixSortProviderImplementation(LightningContext lightningContext)
            : base(lightningContext)
        { }

        #endregion
    }

    sealed partial class CudaRadixSortPairsProviderImplementation : RadixSortPairsProviderImplementation
    {
        #region Instance

        internal CudaRadixSortPairsProviderImplementation(LightningContext lightningContext)
            : base(lightningContext)
        { }

        #endregion
    }
}
