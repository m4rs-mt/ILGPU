// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: CudaScanProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime.Cuda;

namespace ILGPU.Lightning
{
    partial class LightningContext
    {
        partial struct ScanExtension
        {
            public ScanProviderImplementation CreateCudaExtension(CudaAccelerator accelerator)
            {
                return new Cuda.CudaScanProviderImplementation(LightningContext);
            }
        }
    }
}

namespace ILGPU.Lightning.Cuda
{
    sealed partial class CudaScanProviderImplementation : ScanProviderImplementation
    {
        #region Instance

        internal CudaScanProviderImplementation(LightningContext lightningContext)
            : base(lightningContext)
        { }

        #endregion
    }
}
