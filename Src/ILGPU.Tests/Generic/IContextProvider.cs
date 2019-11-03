using ILGPU.IR.Transformations;
using ILGPU.Runtime;

namespace ILGPU.Tests
{
    /// <summary>
    /// Provides contexts for different configurations.
    /// </summary>
    public abstract class ContextProvider
    {
        /// <summary>
        /// Constructs a new context provider.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        protected ContextProvider(OptimizationLevel optimizationLevel)
        {
            OptimizationLevel = optimizationLevel;
        }

        /// <summary>
        /// Returns the associated optimization level.
        /// </summary>
        public OptimizationLevel OptimizationLevel { get; }

        /// <summary>
        /// Creats a new context.
        /// </summary>
        /// <returns>The next context.</returns>
        public virtual Context CreateContext() =>
            new Context(ContextFlags.None, OptimizationLevel);

        /// <summary>
        /// Creats a new accelerator associated to the given context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The created accelerator.</returns>
        public abstract Accelerator CreateAccelerator(Context context);
    }
}
