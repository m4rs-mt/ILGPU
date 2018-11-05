// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Backend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Transformations;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a target platform.
    /// </summary>
    public enum TargetPlatform
    {
        /// <summary>
        /// The X86 target platform.
        /// </summary>
        X86,

        /// <summary>
        /// The X64 target platform.
        /// </summary>
        X64,
    }

    /// <summary>
    /// Represents a general ILGPU backend.
    /// </summary>
    public abstract class Backend : DisposeBase
    {
        #region Nested Types

        /// <summary>
        /// No backend handler.
        /// </summary>
        private readonly struct NoHandler : IBackendHandler
        {
            /// <summary cref="IBackendHandler.FinishedCodeGeneration(IRContext, TopLevelFunction)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool FinishedCodeGeneration(
                IRContext context,
                TopLevelFunction entryPoint) => false;

            /// <summary cref="IBackendHandler.InitializedKernelContext(IRContext, TopLevelFunction)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool InitializedKernelContext(
                IRContext kernelContext,
                TopLevelFunction kernelFunction) => false;

            /// <summary cref="IBackendHandler.PreparedKernelContext(IRContext, TopLevelFunction)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool PreparedKernelContext(
                IRContext kernelContext,
                TopLevelFunction kernelFunction) => false;

            /// <summary cref="IBackendHandler.OptimizedKernelContext(IRContext, TopLevelFunction)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool OptimizedKernelContext(
                IRContext kernelContext,
                TopLevelFunction kernelFunction) => false;
        }

        /// <summary>
        /// Represents the current kernel context in scope of a backend instance.
        /// </summary>
        protected readonly ref struct BackendContext
        {
            /// <summary>
            /// Represents custom backend function information.
            /// </summary>
            public readonly struct FunctionInfo
            {
                internal FunctionInfo(Allocas allocas)
                {
                    Allocas = allocas;
                }

                /// <summary>
                /// Returns the allocation information.
                /// </summary>
                public Allocas Allocas { get; }
            }

            /// <summary>
            /// Enumerates secondary kernel functions in the scope of a backend context.
            /// </summary>
            public struct Enumerator : IEnumerator<FunctionLandscape<FunctionInfo>.Entry>
            {
                private FunctionLandscape<FunctionInfo>.Enumerator enumerator;

                internal Enumerator(in BackendContext context)
                {
                    enumerator = context.FunctionLandscape.GetEnumerator();
                    KernelFunction = context.KernelFunction.Function;
                }

                /// <summary>
                /// Returns the associated main kernel function.
                /// </summary>
                public TopLevelFunction KernelFunction { get; }

                /// <summary>
                /// Returns the current function information.
                /// </summary>
                public FunctionLandscape<FunctionInfo>.Entry Current => enumerator.Current;

                /// <summary cref="IEnumerator.Current" />
                object IEnumerator.Current => Current;

                /// <summary cref="IEnumerator.MoveNext" />
                public bool MoveNext()
                {
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current.Function == KernelFunction)
                            continue;
                        return true;
                    }
                    return false;
                }

                /// <summary cref="IEnumerator.Reset" />
                void IEnumerator.Reset() => throw new InvalidOperationException();

                /// <summary cref="IDisposable.Dispose" />
                public void Dispose()
                {
                    enumerator.Dispose();
                }
            }

            private readonly struct DataProvider : FunctionLandscape<FunctionInfo>.IDataProvider
            {
                /// <summary>
                /// Creates a new data provider.
                /// </summary>
                /// <param name="abi">The current ABI.</param>
                public DataProvider(ABI abi)
                {
                    ABI = abi;
                }

                /// <summary>
                /// Returns the associated ABI.
                /// </summary>
                public ABI ABI { get; }

                /// <summary cref="FunctionLandscape{T}.IDataProvider"/>
                public FunctionInfo GetData(Scope scope, FunctionReferences functionReferences)
                {
                    var allocas = Allocas.Create(scope, ABI);
                    return new FunctionInfo(allocas);
                }
            }

            /// <summary>
            /// Constructs a new backend context.
            /// </summary>
            /// <param name="kernelContext">The current kernel context.</param>
            /// <param name="kernelFunction">The kernel function.</param>
            /// <param name="abi">The current ABI.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal BackendContext(
                IRContext kernelContext,
                TopLevelFunction kernelFunction,
                ABI abi)
            {
                FunctionLandscape = FunctionLandscape<FunctionInfo>.Create(
                    kernelContext.UnsafeTopLevelFunctions,
                    new DataProvider(abi));
                Context = kernelContext;

                KernelFunction = FunctionLandscape[kernelFunction];

                var sharedAllocations = ImmutableArray.CreateBuilder<AllocaInformation>(20);
                var sharedMemorySize = 0;
                foreach (var entry in FunctionLandscape)
                {
                    var allocas = entry.Data.Allocas;
                    sharedAllocations.AddRange(allocas.SharedAllocations.Allocas);
                    sharedMemorySize += allocas.SharedMemorySize;
                }
                SharedAllocations = new AllocaKindInformation(
                    sharedAllocations.ToImmutable(),
                    sharedMemorySize);
            }

            /// <summary>
            /// The associated kernel context.
            /// </summary>
            public IRContext Context { get; }

            /// <summary>
            /// Returns the associated function landscape.
            /// </summary>
            public FunctionLandscape<FunctionInfo> FunctionLandscape { get; }

            /// <summary>
            /// The entry point kernel function.
            /// </summary>
            public FunctionLandscape<FunctionInfo>.Entry KernelFunction { get; }

            /// <summary>
            /// Returns required backend information about the given top level function.
            /// </summary>
            /// <param name="topLevelFunction">The source function.</param>
            /// <returns>Resolved scope and alloca information.</returns>
            public FunctionLandscape<FunctionInfo>.Entry this[TopLevelFunction topLevelFunction] =>
                FunctionLandscape[topLevelFunction];

            /// <summary>
            /// Returns all required shared allocations.
            /// </summary>
            public AllocaKindInformation SharedAllocations { get; }

            /// <summary>
            /// Returns an enumerator to enumerate all entries.
            /// </summary>
            /// <returns>An enumerator to enumerate all entries.</returns>
            public Enumerator GetEnumerator() => new Enumerator(this);
        }

        #endregion

        #region Static

        /// <summary>
        /// Returns the current execution platform.
        /// </summary>
        public static TargetPlatform RuntimePlatform =>
            IntPtr.Size == 8 ? TargetPlatform.X64 : TargetPlatform.X86;

        /// <summary>
        /// Returns the native OS platform.
        /// </summary>
        public static TargetPlatform OSPlatform =>
            Environment.Is64BitOperatingSystem ? TargetPlatform.X64 : TargetPlatform.X86;

        /// <summary>
        /// Returns true iff the current runtime platform is equal to the OS platform.
        /// </summary>
        public static bool RunningOnNativePlatform => RuntimePlatform == OSPlatform;

        /// <summary>
        /// Ensures that the current runtime platform is equal to the OS platform.
        /// If not, this method will throw a <see cref="NotSupportedException"/>.
        /// </summary>
        public static void EnsureRunningOnNativePlatform()
        {
            if (!RunningOnNativePlatform)
                throw new NotSupportedException(string.Format(
                    ErrorMessages.NativePlatformInvocationRequired,
                    RuntimePlatform,
                    OSPlatform));
        }

        /// <summary>
        /// Ensures that the current runtime platform is equal to the given platform.
        /// If not, this method will throw a <see cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="platform">The desired target platform.</param>
        public static void EnsureRunningOnPlatform(TargetPlatform platform)
        {
            if (RuntimePlatform != platform)
                throw new NotSupportedException(string.Format(
                    ErrorMessages.NotSupportedPlatform,
                    RuntimePlatform,
                    platform));
        }

        /// <summary>
        /// Returns either the given target platform or the current one.
        /// </summary>
        /// <param name="platform">The nullable target platform.</param>
        /// <returns>The computed target platform.</returns>
        protected static TargetPlatform GetPlatform(TargetPlatform? platform)
        {
            if (platform.HasValue)
                return platform.Value;
            else
                return RuntimePlatform;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new generic backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="platform">The target platform.</param>
        /// <param name="argumentMapper">The argument mapper.</param>
        protected Backend(
            Context context,
            TargetPlatform platform,
            KernelArgumentMapper argumentMapper)
            : this(context, platform, argumentMapper, null)
        { }

        /// <summary>
        /// Constructs a new generic backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="platform">The target platform.</param>
        /// <param name="argumentMapper">The argument mapper.</param>
        /// <param name="initTransformer">The final transformation initializer (if any).</param>
        protected Backend(
            Context context,
            TargetPlatform platform,
            KernelArgumentMapper argumentMapper,
            Action<Transformer.Builder, int> initTransformer)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Platform = platform;

            ABIProvider = ABIProvider.CreateProvider(platform);
            KernelArgumentMapper = argumentMapper;

            var builder = Transformer.CreateBuilder(TransformerConfiguration.Empty);

            var maxNumIterations = (context.Flags & IRContextFlags.AggressiveInlining) == IRContextFlags.AggressiveInlining ?
                builder.AddOptimizations(new AggressiveInliningConfiguration(), context.OptimizationLevel) :
                builder.AddOptimizations(new DefaultInliningConfiguration(), context.OptimizationLevel);
            initTransformer?.Invoke(builder, maxNumIterations);
            FinalTransformer = builder.ToTransformer();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the assigned context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the target platform.
        /// </summary>
        public TargetPlatform Platform { get; }

        /// <summary>
        /// Returns the current ABI provider of this backend.
        /// </summary>
        public ABIProvider ABIProvider { get; }

        /// <summary>
        /// Returns the associated <see cref="KernelArgumentMapper"/>.
        /// </summary>
        public KernelArgumentMapper KernelArgumentMapper { get; }

        /// <summary>
        /// Returns the transformer that is applied before the final compilation step.
        /// </summary>
        protected Transformer FinalTransformer { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Compiles a given compile unit with the specified entry point using
        /// the given kernel specialization.
        /// </summary>
        /// <param name="entry">The desired entry point.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        public CompiledKernel Compile(
            MethodInfo entry,
            in KernelSpecialization specialization) =>
            Compile(entry, specialization, new NoHandler());

        /// <summary>
        /// Compiles a given compile unit with the specified entry point using
        /// the given kernel specialization.
        /// </summary>
        /// <typeparam name="TBackendHandler">The backend handler type.</typeparam>
        /// <param name="entry">The desired entry point.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="backendHandler">The backend handler.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        public virtual CompiledKernel Compile<TBackendHandler>(
            MethodInfo entry,
            in KernelSpecialization specialization,
            TBackendHandler backendHandler)
            where TBackendHandler : IBackendHandler
        {
            using (var kernelContext = new IRContext(Context, Context.Flags))
            {
                var importSpecification = CreateImportSpecification();
                using (var codeGenerationPhase = Context.BeginCodeGeneration())
                {
                    var irContext = codeGenerationPhase.IRContext;

                    Frontend.CodeGenerationResult generationResult;
                    using (var frontendCodeGenerationPhase = codeGenerationPhase.BeginFrontendCodeGeneration())
                        generationResult = frontendCodeGenerationPhase.GenerateCode(entry);

                    TopLevelFunction function = generationResult.Result;

                    codeGenerationPhase.Optimize();
                    irContext.RefreshFunction(ref function);
                    if (backendHandler.FinishedCodeGeneration(irContext, function))
                        irContext.RefreshFunction(ref function);

                    // Import the all kernel functions into our context
                    kernelContext.Import(irContext, function, importSpecification);
                }

                // Work on the kernel context
                if (!kernelContext.TryGetFunction(entry, out TopLevelFunction kernelFunction))
                    throw new InvalidCodeGenerationException();
                if (backendHandler.InitializedKernelContext(kernelContext, kernelFunction))
                    kernelContext.RefreshFunction(ref kernelFunction);

                using (var abi = ABIProvider.CreateABI(kernelContext))
                {
                    // Prepare the kernel for compilation
                    PrepareKernel(kernelContext, kernelFunction, abi, importSpecification);
                    kernelContext.RefreshFunction(ref kernelFunction);
                    if (backendHandler.PreparedKernelContext(kernelContext, kernelFunction))
                        kernelContext.RefreshFunction(ref kernelFunction);

                    // Apply backend optimizations
                    FinalTransformer.Transform(kernelContext);
                    kernelContext.RefreshFunction(ref kernelFunction);
                    if (backendHandler.OptimizedKernelContext(kernelContext, kernelFunction))
                        kernelContext.RefreshFunction(ref kernelFunction);

                    // Compile kernel
                    kernelContext.UnloadUnreachableMethods(ImmutableArray.Create(
                        kernelFunction));
                    var backendContext = new BackendContext(
                        kernelContext,
                        kernelFunction,
                        abi);
                    var entryPoint = new EntryPoint(
                        kernelFunction.Source as MethodInfo,
                        backendContext.SharedAllocations.TotalSize,
                        specialization);
                    return Compile(entryPoint, abi, backendContext, specialization);
                }
            }
        }

        /// <summary>
        /// Creates a new import specification that is used during the import process
        /// of the actual kernel function.
        /// </summary>
        /// <returns>The created import specification.</returns>
        protected abstract ContextImportSpecification CreateImportSpecification();

        /// <summary>
        /// Applies transformations to the given context.
        /// </summary>
        /// <param name="kernelContext">The kernel context.</param>
        /// <param name="kernelFunction">The current kernel function.</param>
        /// <param name="abi">The current ABI.</param>
        /// <param name="importSpecification">The current import specification.</param>
        protected abstract void PrepareKernel(
            IRContext kernelContext,
            TopLevelFunction kernelFunction,
            ABI abi,
            in ContextImportSpecification importSpecification);

        /// <summary>
        /// Compiles a given compile unit with the specified entry point using
        /// the given kernel specialization and the placement information.
        /// </summary>
        /// <param name="entryPoint">The desired entry point.</param>
        /// <param name="abi">The current ABI.</param>
        /// <param name="backendContext">The current kernel context containing all required functions.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        protected abstract CompiledKernel Compile(
            EntryPoint entryPoint,
            ABI abi,
            in BackendContext backendContext,
            in KernelSpecialization specialization);

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing) { }

        #endregion
    }
}
