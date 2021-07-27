using ILGPU.Runtime.Cuda;
using System;

namespace ILGPU.Tests.Cuda
{
    /// <summary>
    /// An abstract test context for Cuda accelerators.
    /// </summary>
    public abstract class CudaTestContext : TestContext
    {
        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        /// <param name="prepareContext">The context preparation handler.</param>
        protected CudaTestContext(
            OptimizationLevel optimizationLevel,
            Action<Context.Builder> prepareContext)
            : base(
                optimizationLevel,
                builder => prepareContext(builder.Cuda()),
                context => context.CreateCudaAccelerator(0))
        { }

        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        protected CudaTestContext(OptimizationLevel optimizationLevel)
            : this(optimizationLevel, _ => { })
        { }
    }
}
