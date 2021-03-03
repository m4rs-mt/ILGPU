using ILGPU.Runtime.CPU;
using System;

namespace ILGPU.Tests.CPU
{
    /// <summary>
    /// An abstract test context for CPU accelerators.
    /// </summary>
    /// <remarks>
    /// The CPU simulation mode can be change via the environment variable
    /// <see cref="CPUKindEnvVariable"/>. The content should be the raw
    /// string representation of one of the enumeration members of
    /// <see cref="CPUDeviceKind"/>
    /// </remarks>
    public abstract class CPUTestContext : TestContext
    {
        /// <summary>
        /// The name of the environment variable to control the kind of all CPU tests.
        /// </summary>
        public static readonly string CPUKindEnvVariable = "ILGPU_CPU_TEST_KIND";

        /// <summary>
        /// Gets the <see cref="CPUDeviceKind"/> based on the environment variable
        /// <see cref="CPUKindEnvVariable"/>.
        /// </summary>
        /// <remarks>
        /// If the environment variables does not exists or does not contain a valid kind
        /// (specified by the <see cref="CPUDeviceKind"/> enumeration), this
        /// function creates a simulator compatible with the kind
        /// <see cref="CPUDeviceKind.Default"/>.
        /// </remarks>
        private static CPUDeviceKind GetCPUDeviceKind()
        {
            var cpuConfig = Environment.GetEnvironmentVariable(CPUKindEnvVariable);
            return Enum.TryParse(cpuConfig, out CPUDeviceKind kind)
                ? kind
                : CPUDeviceKind.Default;
        }

        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        /// <param name="prepareContext">The context preparation handler.</param>
        protected CPUTestContext(
            OptimizationLevel optimizationLevel,
            Action<Context.Builder> prepareContext)
            : base(
                  optimizationLevel,
                  builder => prepareContext(builder.CPU(GetCPUDeviceKind())),
                  context => context.CreateCPUAccelerator(0))
        { }

        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        public CPUTestContext(OptimizationLevel optimizationLevel)
            : this(optimizationLevel, _ => { })
        { }
    }
}
