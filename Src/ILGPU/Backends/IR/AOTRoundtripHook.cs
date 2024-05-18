using ILGPU.IR;
using System.Collections.Generic;
using System.Linq;

namespace ILGPU.Backends.IR
{
    /// <summary>
    /// IBackendHook implementation for consuming exported IR data.
    /// </summary>
    public sealed class AOTRoundtripHook : IBackendHook
    {
        void IBackendHook.FinishedCodeGeneration(IRContext context, Method entryPoint)
        {
            var temp = context.Export();
        }

        void IBackendHook.InitializedKernelContext(
            IRContext kernelContext,
            Method kernelMethod) { }
        void IBackendHook.OptimizedKernelContext(
            IRContext kernelContext,
            Method kernelMethod) { }
    }
}
