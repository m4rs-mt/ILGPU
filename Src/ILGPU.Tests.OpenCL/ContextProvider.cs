using ILGPU.IR.Transformations;
using ILGPU.Runtime;
using ILGPU.Runtime.OpenCL;

namespace ILGPU.Tests.OpenCL
{
    sealed class OpenCLContextProvider : ContextProvider
    {
        public OpenCLContextProvider(OptimizationLevel optimizationLevel)
            : base(optimizationLevel)
        { }

        public override Accelerator CreateAccelerator(Context context)
        {
            var mainAccelerator = CLAccelerator.CLAccelerators[0];
            return new CLAccelerator(context, mainAccelerator);
        }
    }
}
