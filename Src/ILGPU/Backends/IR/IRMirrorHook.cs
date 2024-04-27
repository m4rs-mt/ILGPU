using ILGPU.IR;

namespace ILGPU.Backends.IR
{
    public class IRMirrorHook : IBackendHook
    {
        public IRContext? CurrentContext { get; private set; }

        public void FinishedCodeGeneration(IRContext context, Method entryPoint)
        {
            CurrentContext?.Dispose();
            CurrentContext = new IRContext(context.Context, true);
            CurrentContext.Import(entryPoint);
        }

        public void InitializedKernelContext(IRContext kernelContext, Method kernelMethod) { }
        public void OptimizedKernelContext(IRContext kernelContext, Method kernelMethod) { }
    }
}
