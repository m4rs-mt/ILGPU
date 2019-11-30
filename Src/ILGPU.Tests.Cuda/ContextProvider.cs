using ILGPU.IR.Transformations;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using System;

namespace ILGPU.Tests.Cuda
{
    sealed class CudaContextProvider : ContextProvider
    {
        public CudaContextProvider(OptimizationLevel optimizationLevel)
            : base(optimizationLevel)
        { }

        public override Accelerator CreateAccelerator(Context context)
        {
            if (CudaAccelerator.CudaAccelerators.Length < 1)
                throw new NotSupportedException();
            return new CudaAccelerator(context);
        }
    }
}
