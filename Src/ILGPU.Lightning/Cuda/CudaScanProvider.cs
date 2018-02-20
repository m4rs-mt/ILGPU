// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: CudaScanProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace ILGPU.Lightning
{
    partial class ScanExtensions
    {
        partial struct ScanExtension
        {
            public ScanProviderImplementation CreateCudaExtension(CudaAccelerator accelerator)
            {
                return new Cuda.CudaScanProviderImplementation(accelerator);
            }
        }
    }
}

namespace ILGPU.Lightning.Cuda
{
    sealed partial class CudaScanProviderImplementation : ScanProviderImplementation
    {
        #region Instance

        internal CudaScanProviderImplementation(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion
    }
}
