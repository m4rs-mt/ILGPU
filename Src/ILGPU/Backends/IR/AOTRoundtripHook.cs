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
        /// <summary>
        /// Returns the <see cref="IRContext"/> instance used to duplicate the original IR. 
        /// </summary>
        public IRContext? ImportContext { get; private set; }

        /// <summary>
        /// Contains the mapping between stale <see cref="Method"/> IDs and their fresh counterparts.
        /// </summary>
        public IReadOnlyDictionary<long, Method>? MethodMapping { get; private set; }

        void IBackendHook.FinishedCodeGeneration(IRContext context, Method entryPoint)
        {
            ImportContext?.Dispose();
            ImportContext = new IRContext(context.Context, true);

            var oldMapping = new Dictionary<long, long>();
            foreach (var method in context.Methods)
            {
                oldMapping.Add(ImportContext.Import(method).Id, method.Id);
            }

            context.ClearCache(ClearCacheMode.Everything);

            var newMapping = context.Import(ImportContext.ExportContainer?.Export() ?? default);

            MethodMapping = newMapping.Where(x => oldMapping.ContainsKey(x.Key)).
                ToDictionary(x => oldMapping[x.Key], x => x.Value);
        }

        void IBackendHook.InitializedKernelContext(IRContext kernelContext, Method kernelMethod) { }
        void IBackendHook.OptimizedKernelContext(IRContext kernelContext, Method kernelMethod) { }
    }
}
