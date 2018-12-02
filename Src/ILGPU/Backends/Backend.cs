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
using ILGPU.IR.Analyses;
using ILGPU.IR.Transformations;
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
            /// <summary cref="IBackendHandler.FinishedCodeGeneration(IRContext, Method)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void FinishedCodeGeneration(IRContext context, Method entryPoint) { }

            /// <summary cref="IBackendHandler.InitializedKernelContext(IRContext, Method)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void InitializedKernelContext(IRContext kernelContext, Method kernelMethod) { }

            /// <summary cref="IBackendHandler.OptimizedKernelContext(IRContext, Method)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OptimizedKernelContext(IRContext kernelContext, Method kernelMethod) { }
        }

        /// <summary>
        /// Represents the current kernel context in scope of a backend instance.
        /// </summary>
        protected readonly ref struct BackendContext
        {
            #region Nested Types

            /// <summary>
            /// An enumerator backend methods.
            /// </summary>
            public struct Enumerator : IEnumerator<(Method, Scope, Allocas)>
            {
                #region Instance

                private UnsafeMethodCollection<MethodCollections.AllMethods>.Enumerator enumerator;
                private readonly CachedScopeProvider scopeProvider;
                private readonly Dictionary<Method, Allocas> allocaMapping;

                /// <summary>
                /// Constructs a new enumerator.
                /// </summary>
                /// <param name="context">The current backend context.</param>
                internal Enumerator(in BackendContext context)
                {
                    KernelMethod = context.KernelMethod;
                    enumerator = context.Context.UnsafeMethods.GetEnumerator();
                    scopeProvider = context.ScopeProvider;
                    allocaMapping = context.allocaMapping;
                }

                #endregion

                #region Properties

                /// <summary>
                /// Returns the associated kernel method.
                /// </summary>
                public Method KernelMethod { get; }

                /// <summary>
                /// Returns the current node.
                /// </summary>
                public (Method, Scope, Allocas) Current
                {
                    get
                    {
                        var method = enumerator.Current;
                        return (method, scopeProvider[method], allocaMapping[method]);
                    }
                }

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                #endregion

                #region Methods

                /// <summary cref="IDisposable.Dispose"/>
                public void Dispose() { }

                /// <summary cref="IEnumerator.MoveNext"/>
                public bool MoveNext()
                {
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current == KernelMethod)
                            continue;
                        return true;
                    }
                    return false;
                }

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();

                #endregion
            }

            #endregion

            #region Instance

            private readonly Dictionary<Method, Allocas> allocaMapping;

            /// <summary>
            /// Constructs a new backend context.
            /// </summary>
            /// <param name="kernelContext">The current kernel context.</param>
            /// <param name="kernelMethod">The kernel function.</param>
            /// <param name="abi">The current ABI.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal BackendContext(
                IRContext kernelContext,
                Method kernelMethod,
                ABI abi)
            {
                Context = kernelContext;
                KernelMethod = kernelMethod;
                ScopeProvider = new CachedScopeProvider();
                allocaMapping = new Dictionary<Method, Allocas>();

                var sharedAllocations = ImmutableArray.CreateBuilder<AllocaInformation>(20);
                var sharedMemorySize = 0;
                foreach (var method in kernelContext.UnsafeMethods)
                {
                    var scope = ScopeProvider[method];
                    var allocas = Allocas.Create(scope, abi);
                    allocaMapping.Add(method, allocas);

                    sharedAllocations.AddRange(allocas.SharedAllocations.Allocas);
                    sharedMemorySize += allocas.SharedMemorySize;
                }
                SharedAllocations = new AllocaKindInformation(
                    sharedAllocations.ToImmutable(),
                    sharedMemorySize);
            }

            #endregion

            /// <summary>
            /// The associated kernel context.
            /// </summary>
            public IRContext Context { get; }

            /// <summary>
            /// Returns the main kernel method.
            /// </summary>
            public Method KernelMethod { get; }

            /// <summary>
            /// Returns the associated kernel scope.
            /// </summary>
            public Scope KernelScope => ScopeProvider[KernelMethod];

            /// <summary>
            /// Returns the associated allocations.
            /// </summary>
            public Allocas KernelAllocas => allocaMapping[KernelMethod];

            /// <summary>
            /// Returns the associated scope provider.
            /// </summary>
            public CachedScopeProvider ScopeProvider { get; }

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
        /// <param name="abi">The current ABI.</param>
        /// <param name="argumentMapper">The argument mapper.</param>
        protected Backend(
            Context context,
            ABI abi,
            ArgumentMapper argumentMapper)
            : this(
                  context,
                  abi,
                  argumentMapper,
                  (_c, _abi) => Transformer.Empty)
        { }

        /// <summary>
        /// Constructs a new generic backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="abi">The current ABI.</param>
        /// <param name="argumentMapper">The argument mapper.</param>
        /// <param name="createKernelTransformer">Creates the target kernel transformer (if any).</param>
        protected Backend(
            Context context,
            ABI abi,
            ArgumentMapper argumentMapper,
            Func<Context, ABI, Transformer> createKernelTransformer)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            ABI = abi ?? throw new ArgumentNullException(nameof(abi));
            ArgumentMapper = argumentMapper;

            if (createKernelTransformer != null)
                KernelTransformer = createKernelTransformer(context, abi);
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
        public TargetPlatform Platform => ABI.TargetPlatform;

        /// <summary>
        /// Returns the current ABI.
        /// </summary>
        public ABI ABI { get; }

        /// <summary>
        /// Returns the associated <see cref="ArgumentMapper"/>.
        /// </summary>
        public ArgumentMapper ArgumentMapper { get; }

        /// <summary>
        /// Returns the transformer that is applied before the final compilation step.
        /// </summary>
        protected Transformer KernelTransformer { get; }

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
                IRContext mainContext;
                Method generatedKernelMethod;
                using (var codeGenerationPhase = Context.BeginCodeGeneration())
                {
                    mainContext = codeGenerationPhase.IRContext;

                    Frontend.CodeGenerationResult generationResult;
                    using (var frontendCodeGenerationPhase = codeGenerationPhase.BeginFrontendCodeGeneration())
                        generationResult = frontendCodeGenerationPhase.GenerateCode(entry);

                    generatedKernelMethod = generationResult.Result;
                    codeGenerationPhase.Optimize();

                    backendHandler.FinishedCodeGeneration(mainContext, generatedKernelMethod);
                }

                // Import the all kernel functions into our context
                var scopeProvider = new CachedScopeProvider();
                var kernelMethod = kernelContext.Import(generatedKernelMethod, scopeProvider);
                backendHandler.InitializedKernelContext(kernelContext, kernelMethod);

                // Apply backend optimizations
                kernelContext.Transform(KernelTransformer);
                backendHandler.OptimizedKernelContext(kernelContext, kernelMethod);

                // Compile kernel
                var backendContext = new BackendContext(kernelContext, kernelMethod, ABI);
                var entryPoint = new EntryPoint(
                    kernelMethod.Source as MethodInfo,
                    backendContext.SharedAllocations.TotalSize,
                    specialization);
                return Compile(entryPoint, backendContext, specialization);
            }
        }

        /// <summary>
        /// Compiles a given compile unit with the specified entry point using
        /// the given kernel specialization and the placement information.
        /// </summary>
        /// <param name="entryPoint">The desired entry point.</param>
        /// <param name="backendContext">The current kernel context containing all required functions.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        protected abstract CompiledKernel Compile(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization);

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing) { }

        #endregion
    }
}
