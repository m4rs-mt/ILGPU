using ILGPU.IR.Transformations;
using ILGPU.Runtime.OpenCL;
using System;

namespace ILGPU.Tests.OpenCL
{
    /// <summary>
    /// An abstract test context for OpenCL accelerators.
    /// </summary>
    public abstract class CLTestContext : TestContext
    {
        /// <summary>
        /// Creates a new OpenCL accelerator for test purposes.
        /// </summary>
        /// <param name="context">The parent context.</param>
        /// <returns>The created accelerator instance.</returns>
        private static CLAccelerator CreateAccelerator(Context context)
        {
            if (CLAccelerator.CLAccelerators.Length < 1)
                throw new NotSupportedException();
            var mainAccelerator = CLAccelerator.CLAccelerators[0];
            return new CLAccelerator(context, mainAccelerator);
        }

        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        /// <param name="prepareContext">The context preparation handler.</param>
        protected CLTestContext(
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
        public CLTestContext(OptimizationLevel optimizationLevel)
            : this(optimizationLevel, null)
        { }
    }
}
