using ILGPU.IR;

namespace ILGPU.Backends.IR
{
    /// <summary>
    /// IBackendHook implementation for consuming exported IR data.
    /// </summary>
    public sealed class IRExporterHook : IBackendHook
    {
        /// <summary>
        /// The <see cref="IRContext"/> most recently used by this exporter hook.
        /// </summary>
        public IRContext? CurrentContext { get; private set; }

        void IBackendHook.FinishedCodeGeneration(IRContext context, Method entryPoint)
        {
            CurrentContext?.Dispose();
            CurrentContext = new IRContext(context.Context, true);
            CurrentContext.Import(entryPoint);
        }

        void IBackendHook.InitializedKernelContext(IRContext kernelContext, Method kernelMethod) { }
        void IBackendHook.OptimizedKernelContext(IRContext kernelContext, Method kernelMethod) { }
    }
}
