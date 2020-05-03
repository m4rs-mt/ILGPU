using ILGPU.IR.Transformations;
using ILGPU.Runtime.OpenCL;
using System;

namespace ILGPU.Tests.OpenCL
{
    public abstract class CLTestContext : TestContext
    {
        private static CLAccelerator CreateAccelerator(Context context)
        {
            if (CLAccelerator.CLAccelerators.Length < 1)
                throw new NotSupportedException();
            var mainAccelerator = CLAccelerator.CLAccelerators[0];
            return new CLAccelerator(context, mainAccelerator);
        }

        public CLTestContext(OptimizationLevel optimizationLevel)
            : base(optimizationLevel, CreateAccelerator)
        { }
    }
}
