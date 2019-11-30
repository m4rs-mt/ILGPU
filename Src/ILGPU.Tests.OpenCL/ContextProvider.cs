using ILGPU.IR.Transformations;
using ILGPU.Runtime;
using ILGPU.Runtime.OpenCL;
using System;

namespace ILGPU.Tests.OpenCL
{
    sealed class OpenCLContextProvider : ContextProvider
    {
        public OpenCLContextProvider(OptimizationLevel optimizationLevel)
            : base(optimizationLevel)
        { }

        public override Accelerator CreateAccelerator(Context context)
        {
            if (CLAccelerator.CLAccelerators.Length < 1)
                throw new NotSupportedException();
            var mainAccelerator = CLAccelerator.CLAccelerators[0];
            return new CLAccelerator(context, mainAccelerator);
        }
    }
}
