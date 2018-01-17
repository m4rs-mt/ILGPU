// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: LLVMBackend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler;
using ILGPU.LLVM;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents event args of a LLVM ILGPU backend.
    /// </summary>
    public sealed class LLVMBackendEventArgs : EventArgs
    {
        internal LLVMBackendEventArgs(
            CompileUnit compileUnit,
            LLVMModuleRef moduleRef)
        {
            CompileUnit = compileUnit;
            ModuleRef = moduleRef;
        }

        /// <summary>
        /// Returns the used compile unit.
        /// </summary>
        public CompileUnit CompileUnit { get; }

        /// <summary>
        /// Returns the current LLVM module.
        /// </summary>
        [CLSCompliant(false)]
        public LLVMModuleRef ModuleRef { get; }
    }

    /// <summary>
    /// Represents a LLVM ILGPU backend.
    /// </summary>
    public abstract class LLVMBackend : Backend
    {
        #region Events

        /// <summary>
        /// Will be invoked when the target-specific backend has generated
        /// a supported entry point. The event argument will represent the
        /// module of the compile unit before any further transformation.
        /// </summary>
        [CLSCompliant(false)]
        public event EventHandler<LLVMBackendEventArgs> PrepareModuleLowering;

        /// <summary>
        /// Will be invoked when the actual kernel code has been generated.
        /// The event argument will point to the kernel-specific module
        /// after all required transformations.
        /// </summary>
        [CLSCompliant(false)]
        public event EventHandler<LLVMBackendEventArgs> KernelModuleLowered;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new LLVM backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="platform">The target platform.</param>
        /// <param name="triple">The platform triple.</param>
        /// <param name="arch">The target architecture.</param>
        protected LLVMBackend(Context context, TargetPlatform platform, string triple, string arch)
            : base(context, platform)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (triple == null)
                throw new ArgumentNullException(nameof(triple));
            if (arch == null)
                throw new ArgumentNullException(nameof(arch));

            GetTargetFromTriple(triple, out LLVMTargetRef targetRef, out IntPtr errorMsg);

            LLVMTarget = targetRef;
            LLVMTargetMachine = CreateTargetMachine(
                targetRef,
                triple,
                arch,
                "",
                LLVMCodeGenOptLevel.LLVMCodeGenLevelAggressive,
                LLVMRelocMode.LLVMRelocDefault,
                LLVMCodeModel.LLVMCodeModelKernel);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the LLVM target of this backend.
        /// </summary>
        [CLSCompliant(false)]
        public LLVMTargetRef LLVMTarget { get; }

        /// <summary>
        /// Returns the LLVM target machine of this backend.
        /// </summary>
        [CLSCompliant(false)]
        public LLVMTargetMachineRef LLVMTargetMachine { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a compatible entry point for this backend.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        /// <param name="entryPoint">The desired entry point.</param>
        /// <param name="entryPointName">The name of the entry point.</param>
        /// <returns>The created entry point.</returns>
        internal abstract LLVMValueRef CreateEntry(
            CompileUnit unit,
            EntryPoint entryPoint,
            out string entryPointName);

        /// <summary>
        /// Prepares the given native module for code generation.
        /// This step can generate required meta information or attributes, for instance.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        /// <param name="module">The final module for code generation.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="generatedEntryPoint">The generated entry point.</param>
        internal abstract void PrepareModule(
            CompileUnit unit,
            LLVMModuleRef module,
            EntryPoint entryPoint,
            LLVMValueRef generatedEntryPoint);

        /// <summary>
        /// Performs the actual linking and code-generation operation.
        /// </summary>
        /// <param name="module">The prepared module for code generation.</param>
        /// <param name="generatedEntryPoint">The entry point.</param>
        /// <returns>The compilation result.</returns>
        internal virtual byte[] Link(LLVMModuleRef module, LLVMValueRef generatedEntryPoint)
        {
            if (TargetMachineEmitToMemoryBuffer(
                LLVMTargetMachine,
                module,
                LLVMCodeGenFileType.LLVMAssemblyFile,
                out IntPtr errorMessage,
                out LLVMMemoryBufferRef buffer).Value != 0 || buffer.Pointer == IntPtr.Zero)
                throw new InvalidOperationException(string.Format(
                    ErrorMessages.CouldNotGenerateMachineCode, Marshal.PtrToStringAnsi(errorMessage)));
            try
            {
                var start = GetBufferStart(buffer);
                var length = GetBufferSize(buffer).ToInt32();
                var data = new byte[length];
                Marshal.Copy(start, data, 0, length);
                return data;
            }
            finally
            {
                DisposeMemoryBuffer(buffer);
            }
        }

        /// <summary cref="Backend.Compile(CompileUnit, MethodInfo)"/>
        public override CompiledKernel Compile(CompileUnit unit, MethodInfo entry, KernelSpecialization specialization)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var entryPoint = new EntryPoint(entry, unit, specialization);
            // Ensure that the entry point is contained in the lib
            var backendEntry = CreateEntry(unit, entryPoint, out string entryPointName);

            PrepareModuleLowering?.Invoke(this, new LLVMBackendEventArgs(unit, unit.LLVMModule));

            var targetModule = CloneModule(unit.LLVMModule);
            try
            {
                // Resolve the cloned version of our entry point
                var targetEntry = GetNamedFunction(targetModule, entryPointName);
                PrepareModule(unit, targetModule, entryPoint, targetEntry);

#if DEBUG
                if (VerifyModule(
                    targetModule,
                    LLVMVerifierFailureAction.LLVMReturnStatusAction,
                    out IntPtr errorMessage).Value != 0)
                    throw new InvalidOperationException(string.Format(
                        ErrorMessages.LLVMModuleVerificationFailed, Marshal.PtrToStringAnsi(errorMessage)));
#endif

                RunPassManager(Context.KernelModulePassManager, targetModule);
                KernelModuleLowered?.Invoke(this, new LLVMBackendEventArgs(unit, targetModule));

                var binaryKernelData = Link(targetModule, targetEntry);
                return new CompiledKernel(Context, entry, binaryKernelData, entryPointName, entryPoint);
            }
            finally
            {
                // Set the entry function to internal linkage
                // to avoid further occurances later on
                SetLinkage(backendEntry, LLVMLinkage.LLVMInternalLinkage);

                DisposeModule(targetModule);
            }
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (LLVMTargetMachine.Pointer != IntPtr.Zero)
            {
                DisposeTargetMachine(LLVMTargetMachine);
                LLVMTargetMachine = default(LLVMTargetMachineRef);
            }
        }

        #endregion
    }
}
