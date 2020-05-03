using ILGPU.IR.Transformations;
using ILGPU.Runtime.Cuda;
using System;

namespace ILGPU.Tests.Cuda
{
    public abstract class CudaTestContext : TestContext
    {
        private static CudaAccelerator CreateAccelerator(Context context)
        {
            if (CudaAccelerator.CudaAccelerators.Length < 1)
                throw new NotSupportedException();
            return new CudaAccelerator(context);
        }

        public CudaTestContext(OptimizationLevel optimizationLevel)
            : base(optimizationLevel, CreateAccelerator)
        { }
    }
}
