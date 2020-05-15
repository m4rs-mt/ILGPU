using ILGPU.IR.Transformations;
using ILGPU.Runtime.CPU;
using System;

namespace ILGPU.Tests.CPU
{
    /// <summary>
    /// An abstract test context for CPU accelerators.
    /// </summary>
    public abstract class CPUTestContext : TestContext
    {
        private static readonly int NumThreads = Math.Max(Math.Min(
            Environment.ProcessorCount, 4), 2);

        public CPUTestContext(OptimizationLevel optimizationLevel)
            : base(
                  optimizationLevel,
                  context => new CPUAccelerator(context, NumThreads))
        { }
    }
}
