using ILGPU.IR.Transformations;
using ILGPU.Runtime.CPU;
using System;

namespace ILGPU.Tests.CPU
{
    public abstract class CPUTestContext : TestContext
    {
        private static int NumThreads = Math.Max(Math.Min(
            Environment.ProcessorCount, 4), 2);

        public CPUTestContext(OptimizationLevel optimizationLevel)
            : base(
                  optimizationLevel,
                  context => new CPUAccelerator(context, NumThreads))
        { }
    }
}
