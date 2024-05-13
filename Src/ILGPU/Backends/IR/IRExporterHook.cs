// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRExporterHook.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

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
            foreach (var method in context.Methods)
            {
                CurrentContext.Import(method);
            }
        }

        void IBackendHook.InitializedKernelContext(
            IRContext kernelContext,
            Method kernelMethod) { }
        void IBackendHook.OptimizedKernelContext(
            IRContext kernelContext,
            Method kernelMethod) { }
    }
}
