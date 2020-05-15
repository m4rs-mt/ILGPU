using ILGPU.IR.Transformations;
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
        /// Creates a new Cuda accelerator for test purposes.
        /// </summary>
        /// <param name="context">The parent context.</param>
        /// <returns>The created accelerator instance.</returns>
        private static CudaAccelerator CreateAccelerator(Context context)
        {
            if (CudaAccelerator.CudaAccelerators.Length < 1)
                throw new NotSupportedException();
            return new CudaAccelerator(context);
        }

        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        /// <param name="prepareContext">The context preparation handler.</param>
        protected CudaTestContext(
            OptimizationLevel optimizationLevel,
            Action<Context> prepareContext)
            : base(
                  optimizationLevel,
                  prepareContext,
                  CreateAccelerator)
        { }

        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        public CudaTestContext(OptimizationLevel optimizationLevel)
            : this(optimizationLevel, null)
        { }
    }
}
