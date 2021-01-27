using ILGPU.IR.Transformations;
using ILGPU.Runtime.CPU;
using System;

namespace ILGPU.Tests.CPU
{
    /// <summary>
    /// An abstract test context for CPU accelerators.
    /// </summary>
    /// <remarks>
    /// The CPU simulation mode can be change via the environment variable
    /// <see cref="CPUTestContext.CPUKindEnvVariable"/>. The content should be the raw
    /// string representation of one of the enumeration members of
    /// <see cref="CPUAcceleratorKind"/>
    /// </remarks>
    public abstract class CPUTestContext : TestContext
    {
        /// <summary>
        /// The name of the environment variable to control the kind of all CPU tests.
        /// </summary>
        public static readonly string CPUKindEnvVariable = "ILGPU_CPU_TEST_KIND";

        /// <summary>
        /// Creates a new CPU accelerator based on the configuration provided via
        /// the environment variable <see cref="CPUKindEnvVariable"/>.
        /// </summary>
        /// <param name="context">The parent context to use.</param>
        /// <returns>The created (parallel) CPU accelerator instance.</returns>
        /// <remarks>
        /// If the environment variables does not exists or does not contain a valid kind
        /// (specified by the <see cref="CPUAcceleratorKind"/> enumeration), this
        /// function creates a simulator compatible with the kind
        /// <see cref="CPUAcceleratorKind.Default"/>.
        /// </remarks>
        private static CPUAccelerator CreateCPUAccelerator(Context context)
        {
            var cpuConfig = Environment.GetEnvironmentVariable(CPUKindEnvVariable);
            if (!Enum.TryParse(cpuConfig, out CPUAcceleratorKind kind))
                kind = CPUAcceleratorKind.Default;
            return CPUAccelerator.Create(context, kind, CPUAcceleratorMode.Parallel);
        }

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
                  CreateCPUAccelerator)
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
