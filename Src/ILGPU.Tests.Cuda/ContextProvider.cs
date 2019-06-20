using ILGPU.IR.Transformations;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace ILGPU.Tests.Cuda
{
    sealed class CudaContextProvider : ContextProvider
    {
        public CudaContextProvider(OptimizationLevel optimizationLevel)
            : base(optimizationLevel)
        { }

        public override Accelerator CreateAccelerator(Context context)
        {
            return new CudaAccelerator(context);
        }
    }
}
