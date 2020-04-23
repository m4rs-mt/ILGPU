using ILGPU.IR.Transformations;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using System;

namespace ILGPU.Algorithms.Tests.CPU
{
    sealed class CPUContextProvider : ContextProvider
    {
        public static int NumThreads = Math.Max(Math.Min(
            Environment.ProcessorCount, 4), 2);

        public CPUContextProvider(OptimizationLevel optimizationLevel)
            : base(optimizationLevel)
        { }

        public override Accelerator CreateAccelerator(Context context)
        {
            return new CPUAccelerator(context, NumThreads);
        }
    }
}
