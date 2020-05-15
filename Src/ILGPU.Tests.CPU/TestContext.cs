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
        /// <summary>
        /// The number of threads to use.
        /// </summary>
        private static readonly int NumThreads = Math.Max(Math.Min(
            Environment.ProcessorCount, 4), 2);

        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        /// <param name="prepareContext">The context preparation handler.</param>
        protected CPUTestContext(
            OptimizationLevel optimizationLevel,
            Action<Context> prepareContext)
            : base(
                  optimizationLevel,
                  prepareContext,
                  context => new CPUAccelerator(context, NumThreads))
        { }

        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        public CPUTestContext(OptimizationLevel optimizationLevel)
            : this(optimizationLevel, null)
        { }
    }
}
