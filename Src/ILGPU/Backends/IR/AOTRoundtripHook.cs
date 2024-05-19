using ILGPU.IR;

namespace ILGPU.Backends.IR
{
    /// <summary>
    /// IBackendHook implementation for consuming exported IR data.
    /// </summary>
    public sealed class AOTRoundtripHook : IBackendHook
    {
        void IBackendHook.FinishedCodeGeneration(IRContext context, Method entryPoint)
        {
            var exported = context.Export();
            context.ClearCache(ClearCacheMode.Everything);
            context.Import(exported);
        }

        void IBackendHook.InitializedKernelContext(
            IRContext kernelContext,
            Method kernelMethod) { }
        void IBackendHook.OptimizedKernelContext(
            IRContext kernelContext,
            Method kernelMethod) { }
    }
}
